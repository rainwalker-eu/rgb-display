Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading

' Used memory graph on the rgb3210 display while running (data from /proc/meminfo on Linux,
' GlobalMemoryStatusEx on Windows).
' One column per sample, newest on the right (or left with --left); column height and color follow
' the used share 1 - available / total, with the CpuGraph palette.
Module Program
    Const W As Integer = 32
    Const H As Integer = 10

    Sub Main(args As String())
        If args.Length > 0 AndAlso (args(0) = "-h" OrElse args(0) = "--help") Then
            Console.WriteLine("Usage: RW.LedDisplay.MemGraph [--flip] [--left] [PORT] [INTERVAL_SEC=1] [BRIGHTNESS_PERCENT=10]")
            Console.WriteLine("  --flip  rotate 180 degrees for a panel mounted upside down")
            Console.WriteLine("  --left  new columns appear on the left and the graph moves right")
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
        Dim interval = TimeSpan.FromSeconds(If(args.Length > 1, Double.Parse(args(1), CultureInfo.InvariantCulture), 1.0))
        Dim brightness = If(args.Length > 2, Integer.Parse(args(2)), 10)

        Dim sender As New DisplaySender With {.SerialPort = port}
        Dim history As New List(Of Double)
        Dim stopping As New ManualResetEventSlim(False)
        AddHandler Console.CancelKeyPress, Sub(s, e)
                                               e.Cancel = True
                                               stopping.Set()
                                           End Sub

        Console.WriteLine($"Used memory graph on {port} every {interval.TotalSeconds}s, Ctrl+C to stop")
        Dim needSetup = True
        Do
            history.Add(ReadUsedFraction())
            If history.Count > W Then history.RemoveAt(0)
            Try
                ' Resent after any failure: a replugged device boots with whatever program is in flash
                If needSetup Then sender.Send(SetupBytes())
                needSetup = False
                Dim image = Render(history, brightness, newestLeft)
                If flip Then image = Rotate180(image)
                sender.Send(DisplayProtocol.PixelBytes(image))
            Catch ex As Exception
                Console.Error.WriteLine(ex.Message)
                needSetup = True
            End Try
        Loop Until stopping.Wait(interval)

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

    Function ReadUsedFraction() As Double
        If RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then Return ReadUsedFractionWindows()
        Return ReadUsedFractionLinux()
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Structure MemoryStatusEx
        Public Length As UInteger
        Public MemoryLoad As UInteger
        Public TotalPhys As ULong
        Public AvailPhys As ULong
        Public TotalPageFile As ULong
        Public AvailPageFile As ULong
        Public TotalVirtual As ULong
        Public AvailVirtual As ULong
        Public AvailExtendedVirtual As ULong
    End Structure

    <DllImport("kernel32.dll", SetLastError:=True)>
    Function GlobalMemoryStatusEx(ByRef status As MemoryStatusEx) As Boolean
    End Function

    ' Available physical memory counts the standby cache as free, like MemAvailable on Linux
    Function ReadUsedFractionWindows() As Double
        Dim status As New MemoryStatusEx With {.Length = CUInt(Marshal.SizeOf(Of MemoryStatusEx)())}
        If Not GlobalMemoryStatusEx(status) Then Throw New IOException($"GlobalMemoryStatusEx failed: {Marshal.GetLastWin32Error()}")
        Return If(status.TotalPhys > 0, 1 - status.AvailPhys / status.TotalPhys, 0)
    End Function

    ' Used = not MemAvailable; MemFree would count page cache as used and keep the graph near full
    Function ReadUsedFractionLinux() As Double
        Dim total As Long = 0, available As Long = 0
        For Each line In File.ReadLines("/proc/meminfo")
            ' "MemAvailable:   12345678 kB"
            Dim parts = line.Split({" "c, ":"c}, StringSplitOptions.RemoveEmptyEntries)
            If parts(0) = "MemTotal" Then total = Long.Parse(parts(1))
            If parts(0) = "MemAvailable" Then available = Long.Parse(parts(1))
        Next
        Return If(total > 0, 1 - available / total, 0)
    End Function

    Function Render(history As List(Of Double), brightness As Integer, newestLeft As Boolean) As Color(,)
        Dim image(W - 1, H - 1) As Color
        For i = 0 To history.Count - 1
            Dim x = If(newestLeft, history.Count - 1 - i, W - history.Count + i)
            Dim level = history(i) * H
            Dim color = LoadColor(history(i))
            For y = 0 To H - 1 ' from the bottom
                ' The top pixel lights partially; PWM is linear in light, so coverage maps directly
                Dim coverage = Math.Min(1.0, Math.Max(0.0, level - y))
                image(x, H - 1 - y) = Scale(color, coverage * brightness / 100)
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

    ' Same palette as CpuGraph: hue goes green -> yellow -> red and value rises with the used share
    Function LoadColor(load As Double) As Color
        Dim hue = 120 * (1 - load)
        Dim value = 0.3 + 0.7 * load
        Dim r = If(hue >= 60, (120 - hue) / 60, 1.0)
        Dim g = If(hue >= 60, 1.0, hue / 60)
        Return Color.FromArgb(CInt(255 * r * value), CInt(255 * g * value), 0)
    End Function

    Function Scale(c As Color, k As Double) As Color
        Return Color.FromArgb(CInt(c.R * k), CInt(c.G * k), CInt(c.B * k))
    End Function
End Module
