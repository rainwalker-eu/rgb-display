/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : Main program body
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2026 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include "main.h"
#include "spi.h"
#include "tim.h"
#include "usart.h"
#include "usb_device.h"
#include "gpio.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */
#include <string.h>
#include "stm32g4xx_ll_adc.h"
#include "usbd_cdc_if.h"

/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */

/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */
#define LED_W 32
#define LED_H 10
#define FRAMES 7
#define PROGRAM_STEPS 128
#define FRAME_SIZE (LED_W * LED_H * 3)
#define PIXEL_DATA_SIZE (FRAME_SIZE * FRAMES)
#define PROGRAM_START 0
#define PROGRAM_SIZE PROGRAM_STEPS * 4
#define PIXEL_START PROGRAM_SIZE
#define PIXEL_LINE_SIZE (LED_W * FRAMES * 3)
/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/

/* USER CODE BEGIN PV */
union
{
	struct 
	{
		struct 
		{
			uint8_t command;
			uint8_t param1;
			uint8_t param2;
			uint8_t param3;
		} __attribute__((__packed__)) program[PROGRAM_STEPS];
		uint8_t pixeldata[PIXEL_DATA_SIZE];
	} __attribute__((__packed__)) data;
	uint64_t raw64[(PIXEL_DATA_SIZE + PROGRAM_SIZE) / 8];
	uint8_t raw8[PIXEL_DATA_SIZE + PROGRAM_SIZE];
} data;

uint32_t dataIndex = 0;
uint8_t command = 0;
uint8_t display[LED_W * LED_H * 3];
uint32_t volatile programCounter = 0;
uint8_t volatile waiting = 0;
uint8_t volatile lastSerialByte = 0;
int brightness = 1;
uint32_t volatile thermalScale = 256; // output scale from ThermalCheck, 256 = full brightness
int32_t temperature16 = 25 * 16;      // filtered MCU die temperature, 1/16 degC
/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
/* USER CODE BEGIN PFP */

/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */
void  FlashErase(void)
{
	HAL_FLASH_Unlock();
	FLASH_EraseInitTypeDef eraseInit;
	eraseInit.TypeErase =FLASH_TYPEERASE_PAGES;
	eraseInit.Page = 12;
	eraseInit.Banks = FLASH_BANK_1;
	eraseInit.NbPages = 4;
	uint32_t PageError = 0;
	HAL_FLASHEx_Erase(&eraseInit, &PageError);
	HAL_FLASH_Lock();
}

void FlashWrite(void)
{
	HAL_FLASH_Unlock();	
	for (uint32_t i = 0; i < sizeof(data.raw64)/8; i++)
	{
		uint64_t b4 = data.raw64[i];
		HAL_FLASH_Program(FLASH_TYPEPROGRAM_DOUBLEWORD, 0x8006000  + i * 8, b4);
	}
	HAL_FLASH_Lock();
}

void FlashRead(void)
{
	for (uint32_t i = 0; i < sizeof(data.raw64)/8; i++)
	{
		uint64_t b4 = *(uint64_t*)(0x8006000 + i * 8);
		data.raw64[i] = b4;
	}
}

// Bit timing in TIM17 ticks (84 MHz, ~11.9 ns), counted from the rising edge of each bit.
// XL-1010RGBC-WS2812B datasheet minimums: T0H 0.3 us, T1H 0.9 us, T0L 0.9 us, T1L 0.3 us,
// period 1.2 us, low time under 20 us, reset over 200 us
#define T0H_TICKS 30      // ~0.36 us, high time of a 0 bit
#define T1H_TICKS 80      // ~0.95 us, high time of a 1 bit
#define TBIT_TICKS 110    // ~1.31 us, bit period: T0L ~0.95 us, T1L ~0.36 us; mask math may stretch the low phase
#define TRESET_TICKS 25200 // ~300 us

// LED0..LED5 are on GPIOA, LED6..LED9 on GPIOB (see main.h); one register write per port
// keeps edges identical on all rows, so pulse widths no longer depend on row order
#define LEDS_A (LED0_Pin | LED1_Pin | LED2_Pin | LED3_Pin | LED4_Pin | LED5_Pin)
#define LEDS_B (LED6_Pin | LED7_Pin | LED8_Pin | LED9_Pin)

void DisplayUpdate(void)
{
	// Thermal limit is applied on output, so it covers frames, Fill and scrolls alike
	uint32_t scale = thermalScale;
	__disable_irq();
	LL_TIM_EnableCounter(TIM17);
	GPIOA->BRR = LEDS_A;
	GPIOB->BRR = LEDS_B;
	TIM17->CNT = 0; while (TIM17->CNT < TRESET_TICKS) {}
	TIM17->CNT = TBIT_TICKS;

	for (int x = 0; x < 96; x++)
	{
		{
			uint8_t b0 =	 (display[x + 0 * LED_W*3] * scale) >> 8;
			uint8_t b1 =	 (display[x + 1 * LED_W*3] * scale) >> 8;
			uint8_t b2 =	 (display[x + 2 * LED_W*3] * scale) >> 8;
			uint8_t b3 =	 (display[x + 3 * LED_W*3] * scale) >> 8;
			uint8_t b4 =	 (display[x + 4 * LED_W*3] * scale) >> 8;
			uint8_t b5 =	 (display[x + 5 * LED_W*3] * scale) >> 8;
			uint8_t b6 =	 (display[x + 6 * LED_W*3] * scale) >> 8;
			uint8_t b7 =	 (display[x + 7 * LED_W*3] * scale) >> 8;
			uint8_t b8 =	 (display[x + 8 * LED_W*3] * scale) >> 8;
			uint8_t b9 =	 (display[x + 9 * LED_W*3] * scale) >> 8;

			for (uint8_t i = 0;i < 8; i++)
			{
				uint8_t mask = 128 >> i;
				uint32_t zeroA = 0, zeroB = 0;
				if ((b0 & mask) == 0) zeroA |= LED0_Pin;
				if ((b1 & mask) == 0) zeroA |= LED1_Pin;
				if ((b2 & mask) == 0) zeroA |= LED2_Pin;
				if ((b3 & mask) == 0) zeroA |= LED3_Pin;
				if ((b4 & mask) == 0) zeroA |= LED4_Pin;
				if ((b5 & mask) == 0) zeroA |= LED5_Pin;
				if ((b6 & mask) == 0) zeroB |= LED6_Pin;
				if ((b7 & mask) == 0) zeroB |= LED7_Pin;
				if ((b8 & mask) == 0) zeroB |= LED8_Pin;
				if ((b9 & mask) == 0) zeroB |= LED9_Pin;

				while (TIM17->CNT < TBIT_TICKS) {}
				TIM17->CNT = 0;
				GPIOA->BSRR = LEDS_A;
				GPIOB->BSRR = LEDS_B;
				while (TIM17->CNT < T0H_TICKS) {}
				GPIOA->BRR = zeroA;
				GPIOB->BRR = zeroB;
				while (TIM17->CNT < T1H_TICKS) {}
				GPIOA->BRR = LEDS_A;
				GPIOB->BRR = LEDS_B;
			}
		}
	}
	__enable_irq();
}

// Thermal protection by the MCU die sensor, which follows the board with a lag: LEDs are rated
// for -40..+85 degC ambient (XL-1010RGBC-WS2812B datasheet), so limiting starts well below that
#define THERMAL_START_C 55   // full brightness up to here
#define THERMAL_FULL_C 75    // linear down to THERMAL_MIN_SCALE here; also the restart point after cutoff
#define THERMAL_OFF_C 80     // display off from here
#define THERMAL_MIN_SCALE 26 // ~10% of 256
#define THERMAL_PERIOD_MS 1000

static uint16_t AdcRead(uint32_t channel)
{
	LL_ADC_REG_SetSequencerRanks(ADC1, LL_ADC_REG_RANK_1, channel);
	LL_ADC_ClearFlag_EOC(ADC1);
	LL_ADC_REG_StartConversion(ADC1);
	while (!LL_ADC_IsActiveFlag_EOC(ADC1)) {}
	return LL_ADC_REG_ReadConversionData12(ADC1);
}

// Factory calibration is taken at VREF+ = 3.0 V, so the actual VDDA is measured through VREFINT first
static int32_t ReadTemperature(void)
{
	uint32_t vrefint = AdcRead(LL_ADC_CHANNEL_VREFINT);
	uint32_t sensor = AdcRead(LL_ADC_CHANNEL_TEMPSENSOR_ADC1);
	uint32_t vdda = __LL_ADC_CALC_VREFANALOG_VOLTAGE(vrefint, LL_ADC_RESOLUTION_12B);
	return __LL_ADC_CALC_TEMPERATURE(vdda, sensor, LL_ADC_RESOLUTION_12B);
}

// ADC1 set up here with LL instead of CubeMX: internal channels need no pins and the synchronous
// HCLK/4 clock (42 MHz, under the ADC limit) needs no RCC kernel clock selection
void ThermalInit(void)
{
	LL_AHB2_GRP1_EnableClock(LL_AHB2_GRP1_PERIPH_ADC12);
	LL_ADC_SetCommonClock(ADC12_COMMON, LL_ADC_CLOCK_SYNC_PCLK_DIV4);
	LL_ADC_SetCommonPathInternalCh(ADC12_COMMON, LL_ADC_PATH_INTERNAL_VREFINT | LL_ADC_PATH_INTERNAL_TEMPSENSOR);
	LL_ADC_DisableDeepPowerDown(ADC1);
	LL_ADC_EnableInternalRegulator(ADC1);
	LL_mDelay(1); // regulator 20 us, temperature sensor 120 us
	LL_ADC_StartCalibration(ADC1, LL_ADC_SINGLE_ENDED);
	while (LL_ADC_IsCalibrationOnGoing(ADC1)) {}
	LL_mDelay(1); // a few ADC cycles between calibration and enable
	// 640.5 cycles at 42 MHz ~ 15 us, above the sensor and VREFINT minimum sampling times
	LL_ADC_SetChannelSamplingTime(ADC1, LL_ADC_CHANNEL_TEMPSENSOR_ADC1, LL_ADC_SAMPLINGTIME_640CYCLES_5);
	LL_ADC_SetChannelSamplingTime(ADC1, LL_ADC_CHANNEL_VREFINT, LL_ADC_SAMPLINGTIME_640CYCLES_5);
	LL_ADC_ClearFlag_ADRDY(ADC1);
	LL_ADC_Enable(ADC1);
	while (!LL_ADC_IsActiveFlag_ADRDY(ADC1)) {}
	int32_t t = ReadTemperature();
	if (t != LL_ADC_TEMPERATURE_CALC_ERROR) temperature16 = t * 16;
}

// Called from the main loop and from long waits inside program steps, so it also runs while a program
// is stopped or waiting; when the limit changes the last frame is redrawn with it
void ThermalCheck(void)
{
	static uint32_t lastTick = 0;
	if (HAL_GetTick() - lastTick < THERMAL_PERIOD_MS) return;
	lastTick = HAL_GetTick();
	int32_t t = ReadTemperature();
	if (t == LL_ADC_TEMPERATURE_CALC_ERROR) return;
	temperature16 += (t * 16 - temperature16) / 4; // ~4 s time constant against ADC noise

	uint32_t scale;
	if (temperature16 >= THERMAL_OFF_C * 16 || (thermalScale == 0 && temperature16 > THERMAL_FULL_C * 16))
		scale = 0;
	else if (temperature16 <= THERMAL_START_C * 16)
		scale = 256;
	else if (temperature16 >= THERMAL_FULL_C * 16)
		scale = THERMAL_MIN_SCALE;
	else
		scale = 256 - (256 - THERMAL_MIN_SCALE) * (temperature16 - THERMAL_START_C * 16)
			/ ((THERMAL_FULL_C - THERMAL_START_C) * 16);
	if (scale != thermalScale)
	{
		thermalScale = scale;
		DisplayUpdate();
	}
}

static char* AppendInt(char* p, int32_t value)
{
	char digits[11];
	int n = 0;
	if (value < 0) { *p++ = '-'; value = -value; }
	do { digits[n++] = '0' + value % 10; value /= 10; } while (value);
	while (n) *p++ = digits[--n];
	return p;
}

// Reply to 170,130: "temp=<degC> limit=<percent>\n"; the buffer must outlive the USB transfer
void ReportStatus(void)
{
	static char reply[32];
	char* p = reply;
	memcpy(p, "temp=", 5); p += 5;
	p = AppendInt(p, temperature16 / 16);
	memcpy(p, " limit=", 7); p += 7;
	p = AppendInt(p, thermalScale * 100 / 256);
	*p++ = '\n';
	CDC_Transmit_FS((uint8_t*)reply, p - reply);
}

void DisplaySet(int x, int y, uint8_t r, uint8_t g, uint8_t b)
{
	if (x < 0 || x >= 32 || y < 0 || y >= 10) return;
	int index = (y * 96 + x * 3);
	display[index] = g;
	display[index + 1] = r;
	display[index + 2] = b;
}


void DisplayClear(uint8_t r )
{
	for (int i = 0; i < 32 * 10 * 3; i ++)
	{
		display[i] = r;
	}
}

void DisplayFill(uint8_t r, uint8_t g, uint8_t b)
{
	for (int i = 0; i < 32 * 10 * 3; i += 3)
	{
		display[i + 0] = g;
		display[i + 1] = r;
		display[i + 2] = b;
	}
}

void ProcessSerialData(uint8_t* buf, uint32_t length)
{
	for (uint32_t i = 0; i < length; i++)
	{
		uint8_t byte = buf[i];
		if (lastSerialByte == 170)
		{
			switch (byte)
			{
			case 0:
				if (dataIndex < sizeof(data.raw8))
				{
					data.raw8[dataIndex++] = 170;
				}		
				break;
			case 16:
				dataIndex = 0;
				break;
			case 17:dataIndex = PIXEL_START;break;	
			case 18:dataIndex = PIXEL_START + PIXEL_LINE_SIZE * 1;break;
			case 19:dataIndex = PIXEL_START + PIXEL_LINE_SIZE * 2;break;
			case 20:dataIndex = PIXEL_START + PIXEL_LINE_SIZE * 3;break;
			case 21:dataIndex = PIXEL_START + PIXEL_LINE_SIZE * 4;break;
			case 22:dataIndex = PIXEL_START + PIXEL_LINE_SIZE * 5;break;
			case 23:dataIndex = PIXEL_START + PIXEL_LINE_SIZE * 6;break;
			case 24:dataIndex = PIXEL_START + PIXEL_LINE_SIZE * 7;break;
			case 25:dataIndex = PIXEL_START + PIXEL_LINE_SIZE * 8;break;
			case 26:dataIndex = PIXEL_START + PIXEL_LINE_SIZE * 9;break;
			
			case 64:
				programCounter = 0;
				waiting = 0;
				break;
			case 65:
				programCounter = 10000;
				waiting	= 0;
				break;			
			case 66:
				waiting = 0;
				break;
			case 128:
			case 129:
			case 130:
				command = byte;
				break;
			}
		}
		else
		{
			if (dataIndex < sizeof(data.raw8))
			{
				data.raw8[dataIndex++] = byte;
			}		
		}
		lastSerialByte = byte;
	}
}

void ProcessCommand(void)
{
	if (command == 128)
	{
		DisplayClear(0);
		DisplayUpdate();
	}
	if (command == 129)
	{
		FlashErase();
		FlashWrite();
	}
	if (command == 130)
	{
		ReportStatus();
	}
	command = 0;
}

void CopyPixelData(int offset)
{
	for (int x = 0; x < LED_W * 3; x++)
	{
		for (int y = 0; y < LED_H; y++)
		{
			display[y * LED_W * 3 + x] = data.data.pixeldata[offset + x + PIXEL_LINE_SIZE * y] * brightness/100;
		}
	}
}

void WaitForButton(void)
{
	waiting = 1;
	while (LL_GPIO_IsInputPinSet(BTN_GPIO_Port, BTN_Pin) == 0 &&
		   LL_GPIO_IsInputPinSet(SS_GPIO_Port, SS_Pin)	 == 1 && waiting == 1) { ThermalCheck(); }
	LL_mDelay(50);
	while ((LL_GPIO_IsInputPinSet(BTN_GPIO_Port, BTN_Pin) == 1 ||
		   LL_GPIO_IsInputPinSet(SS_GPIO_Port, SS_Pin)	 == 0) && waiting == 1) { ThermalCheck(); }
	LL_mDelay(50);
	waiting = 0;
}

void ProcessProgram(void)
{
	if (programCounter < 128)
	{
		uint8_t cmd = data.data.program[programCounter].command;
		uint8_t p1 = data.data.program[programCounter].param1;
		uint8_t p2 = data.data.program[programCounter].param2;
		uint8_t p3 = data.data.program[programCounter].param3;
		programCounter++;
		switch (cmd)
		{
		case 0:
		case 255:
			// NOP
			break;
		case 1:
			//brightness
			brightness = p1;
			break;
		case 8:
			DisplayClear(0);
			DisplayUpdate();
			break;
		case 9:
			//fill
			DisplayFill(p1, p2, p3);
			DisplayUpdate();
			break;
		case 16:
			//start from frame p1
			CopyPixelData(p1 * LED_W*3);
			DisplayUpdate();
			break;
		case 17:
			//start from x = p1
			CopyPixelData(p1*3);
			DisplayUpdate();
			break;
		case 128:
			//scrollleft
			for (int offset = LED_W * 3*p1; offset<LED_W*3*(p2); offset+=3)
			{
				CopyPixelData(offset);
				DisplayUpdate();
				LL_mDelay(p3);
				ThermalCheck();
			}
			break;
		case 129:
			//scrollright
			for (int offset = LED_W * 3*p2; offset > LED_W * 3*(p1); offset -= 3)
			{
				CopyPixelData(offset);
				DisplayUpdate();
				LL_mDelay(p3);
				ThermalCheck();
			}
			break;		
		case 64:
			//wait button			
			WaitForButton();
			break;
		case 65:
			//wait p1 ms
			waiting = 1;
			LL_mDelay(p1);
			waiting = 0;
			break;
		case 66:
			//wait p1 seconds
			waiting = 1;
			for (int i = 0; (i < p1*10) && waiting == 1; i++)
			{
				LL_mDelay(100);
				ThermalCheck();
			}
			waiting = 0;
			break;
		case 254:
			//restart
			programCounter = 0;
			break;
		}
	}
}

/* USER CODE END 0 */

/**
  * @brief  The application entry point.
  * @retval int
  */
int main(void)
{

  /* USER CODE BEGIN 1 */

  /* USER CODE END 1 */

  /* MCU Configuration--------------------------------------------------------*/

  /* Reset of all peripherals, Initializes the Flash interface and the Systick. */
  HAL_Init();

  /* USER CODE BEGIN Init */

  /* USER CODE END Init */

  /* Configure the system clock */
  SystemClock_Config();

  /* USER CODE BEGIN SysInit */

  /* USER CODE END SysInit */

  /* Initialize all configured peripherals */
  MX_GPIO_Init();
  MX_USB_Device_Init();
  MX_TIM17_Init();
  MX_SPI1_Init();
  MX_USART1_UART_Init();
  /* USER CODE BEGIN 2 */
  DisplayClear(0);
  DisplayUpdate();
  FlashRead();
  ThermalInit();
  /* USER CODE END 2 */

  /* Infinite loop */
  /* USER CODE BEGIN WHILE */
  while (1)
  {
    /* USER CODE END WHILE */

    /* USER CODE BEGIN 3 */
	  ProcessProgram();
	  ProcessCommand();
	  ThermalCheck();
  }
  /* USER CODE END 3 */
}

/**
  * @brief System Clock Configuration
  * @retval None
  */
void SystemClock_Config(void)
{
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};

  /** Configure the main internal regulator output voltage
  */
  HAL_PWREx_ControlVoltageScaling(PWR_REGULATOR_VOLTAGE_SCALE1_BOOST);

  /** Initializes the RCC Oscillators according to the specified parameters
  * in the RCC_OscInitTypeDef structure.
  */
  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_HSI48|RCC_OSCILLATORTYPE_HSE;
  RCC_OscInitStruct.HSEState = RCC_HSE_ON;
  RCC_OscInitStruct.HSI48State = RCC_HSI48_ON;
  RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;
  RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSE;
  RCC_OscInitStruct.PLL.PLLM = RCC_PLLM_DIV2;
  RCC_OscInitStruct.PLL.PLLN = 21;
  RCC_OscInitStruct.PLL.PLLP = RCC_PLLP_DIV2;
  RCC_OscInitStruct.PLL.PLLQ = RCC_PLLQ_DIV6;
  RCC_OscInitStruct.PLL.PLLR = RCC_PLLR_DIV2;
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)
  {
    Error_Handler();
  }

  /** Initializes the CPU, AHB and APB buses clocks
  */
  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK|RCC_CLOCKTYPE_SYSCLK
                              |RCC_CLOCKTYPE_PCLK1|RCC_CLOCKTYPE_PCLK2;
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_PLLCLK;
  RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;
  RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV1;
  RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV1;

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_4) != HAL_OK)
  {
    Error_Handler();
  }
}

/* USER CODE BEGIN 4 */

/* USER CODE END 4 */

/**
  * @brief  This function is executed in case of error occurrence.
  * @retval None
  */
void Error_Handler(void)
{
  /* USER CODE BEGIN Error_Handler_Debug */
  /* User can add his own implementation to report the HAL error return state */
  __disable_irq();
  while (1)
  {
  }
  /* USER CODE END Error_Handler_Debug */
}
#ifdef USE_FULL_ASSERT
/**
  * @brief  Reports the name of the source file and the source line number
  *         where the assert_param error has occurred.
  * @param  file: pointer to the source file name
  * @param  line: assert_param error line source number
  * @retval None
  */
void assert_failed(uint8_t *file, uint32_t line)
{
  /* USER CODE BEGIN 6 */
  /* User can add his own implementation to report the file name and line number,
     ex: printf("Wrong parameters value: file %s on line %d\r\n", file, line) */
  /* USER CODE END 6 */
}
#endif /* USE_FULL_ASSERT */
