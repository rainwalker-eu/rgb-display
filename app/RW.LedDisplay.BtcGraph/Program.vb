Imports System.Drawing
Imports System.Globalization
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading

' BTC/USDT price graph on the rgb3210 display while running, candles from the public Binance API (no key).
' One column per candle, newest on the right (or left with --left); height is the close price scaled to
' the window's range, green for a candle that closed up, red for down.
Module Program
    Const W As Integer = 32
    Const H As Integer = 10
    Const KlinesUrl As String = "https://api.binance.com/api/v3/klines?symbol=BTCUSDT"
    ReadOnly PollInterval As TimeSpan = TimeSpan.FromMinutes(1)

    Structure Candle
        Public Open As Double
        Public Close As Double
    End Structure

    Sub Main(args As String())
        If args.Length > 0 AndAlso (args(0) = "-h" OrElse args(0) = "--help") Then
            Console.WriteLine("Usage: RW.LedDisplay.BtcGraph [--flip] [--left] [PORT] [CANDLE=1h] [BRIGHTNESS_PERCENT=10]")
            Console.WriteLine("  --flip  rotate 180 degrees for a panel mounted upside down")
            Console.WriteLine("  --left  new candles appear on the left and the graph moves right")
            Console.WriteLine("  CANDLE  Binance kline interval: 1m 3m 5m 15m 30m 1h 2h 4h 6h 8h 12h 1d 3d 1w 1M")
            Console.WriteLine("  PORT    serial port; default /dev/ttyACM0, on Windows the COM port of the first display found")
            Return
        End If
        Dim flip = args.Contains("--flip")
        Dim newestLeft = args.Contains("--left")
        args = args.Where(Function(a) a <> "--flip" AndAlso a <> "--left").ToArray()
        Dim port = If(args.Length > 0, args(0), DisplayPorts.DefaultPort())
        If port Is Nothing Then
            Console.Error.WriteLine("No display found, pass its serial port as the first argument")
            Environment.ExitCode = 1
            Return
        End If
        Dim candleInterval = If(args.Length > 1, args(1), "1h")
        Dim brightness = If(args.Length > 2, Integer.Parse(args(2)), 10)

        Dim sender As New DisplaySender With {.SerialPort = port}
        Dim http As New HttpClient With {.Timeout = TimeSpan.FromSeconds(15)}
        Dim stopping As New ManualResetEventSlim(False)
        AddHandler Console.CancelKeyPress, Sub(s, e)
                                               e.Cancel = True
                                               stopping.Set()
                                           End Sub

        Console.WriteLine($"BTC/USDT {candleInterval} candles on {port}, updated every {PollInterval.TotalSeconds}s, Ctrl+C to stop")
        Dim needSetup = True
        Do
            Dim candles As List(Of Candle)
            Try
                candles = FetchCandles(http, candleInterval)
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} {candles.Last().Close.ToString("F2", CultureInfo.InvariantCulture)} USDT")
            Catch ex As Exception
                ' The display keeps the last picture until the next successful fetch
                Console.Error.WriteLine($"Fetch failed: {ex.Message}")
                Continue Do
            End Try
            Try
                ' Resent after any failure: a replugged device boots with whatever program is in flash
                If needSetup Then sender.Send(SetupBytes())
                needSetup = False
                Dim image = Render(candles, brightness, newestLeft)
                If flip Then image = Rotate180(image)
                sender.Send(DisplayProtocol.PixelBytes(image))
            Catch ex As Exception
                Console.Error.WriteLine(ex.Message)
                needSetup = True
            End Try
        Loop Until stopping.Wait(PollInterval)

        Try
            sender.Send(DisplayProtocol.StopProgram(), DisplayProtocol.ClearDisplay())
        Catch ex As Exception
            Console.Error.WriteLine(ex.Message)
        End Try
    End Sub

    ' Pixel data reaches the LEDs only through program steps, and the firmware boots with brightness 1
    Function SetupBytes() As Byte()
        Dim program = DisplayProtocol.ProgramBytes({
            New DisplayCommand(DisplayCommandType.Bright, 100),
            New DisplayCommand(DisplayCommandType.Frame, 0),
            New DisplayCommand(DisplayCommandType.Msec, 20)}) ' lets USB through between frames
        Return DisplayProtocol.StopProgram().Concat(program).Concat(DisplayProtocol.StartProgram()).ToArray()
    End Function

    ' The last kline is the current, still open candle, so the newest column moves with the price
    Function FetchCandles(http As HttpClient, candleInterval As String) As List(Of Candle)
        Dim json = http.GetStringAsync($"{KlinesUrl}&interval={Uri.EscapeDataString(candleInterval)}&limit={W}").GetAwaiter().GetResult()
        Dim candles As New List(Of Candle)
        Using doc = JsonDocument.Parse(json)
            ' [openTime, "open", "high", "low", "close", "volume", closeTime, ...]
            For Each k In doc.RootElement.EnumerateArray()
                candles.Add(New Candle With {
                    .Open = Double.Parse(k(1).GetString(), CultureInfo.InvariantCulture),
                    .Close = Double.Parse(k(4).GetString(), CultureInfo.InvariantCulture)})
            Next
        End Using
        If candles.Count = 0 Then Throw New IO.InvalidDataException("no candles in the response")
        Return candles
    End Function

    Function Render(candles As List(Of Candle), brightness As Integer, newestLeft As Boolean) As Color(,)
        Dim image(W - 1, H - 1) As Color
        Dim low = candles.Min(Function(c) c.Close)
        Dim high = candles.Max(Function(c) c.Close)
        For i = 0 To candles.Count - 1
            Dim x = If(newestLeft, candles.Count - 1 - i, W - candles.Count + i)
            ' The window's lowest close still lights one pixel, the highest fills the column
            Dim level = 1 + If(high > low, (candles(i).Close - low) / (high - low), 1) * (H - 1)
            Dim barColor = If(candles(i).Close >= candles(i).Open, Color.FromArgb(0, 255, 0), Color.FromArgb(255, 0, 0))
            For y = 0 To H - 1 ' from the bottom
                ' The top pixel lights partially; PWM is linear in light, so coverage maps directly
                Dim coverage = Math.Min(1.0, Math.Max(0.0, level - y))
                image(x, H - 1 - y) = Scale(barColor, coverage * brightness / 100)
            Next
        Next
        Return image
    End Function

    Function Rotate180(image As Color(,)) As Color(,)
        Dim rotated(W - 1, H - 1) As Color
        For x = 0 To W - 1
            For y = 0 To H - 1
                rotated(W - 1 - x, H - 1 - y) = image(x, y)
            Next
        Next
        Return rotated
    End Function

    Function Scale(c As Color, k As Double) As Color
        Return Color.FromArgb(CInt(c.R * k), CInt(c.G * k), CInt(c.B * k))
    End Function
End Module
