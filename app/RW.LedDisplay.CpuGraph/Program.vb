Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading

' Per-core CPU load bars on the rgb3210 display while running (data from /proc/stat on Linux,
' NtQuerySystemInformation on Windows).
' One bar per core, core 0 on the left (on the right with --left); bar height and color follow the load.
Module Program
    Const W As Integer = 32
    Const H As Integer = 10

    Sub Main(args As String())
        If args.Length > 0 AndAlso (args(0) = "-h" OrElse args(0) = "--help") Then
            Console.WriteLine("Usage: RW.LedDisplay.CpuGraph [--flip] [--left] [PORT] [INTERVAL_SEC=1] [BRIGHTNESS_PERCENT=10]")
            Console.WriteLine("  --flip  rotate 180 degrees for a panel mounted upside down")
            Console.WriteLine("  --left  mirror horizontally: core 0 on the right")
            Console.WriteLine("  PORT    serial port; default /dev/ttyACM0, on Windows the COM port of the first display found")
            Return
        End If
        Dim flip = args.Contains("--flip")
        Dim mirror = args.Contains("--left")
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
        Dim stopping As New ManualResetEventSlim(False)
        AddHandler Console.CancelKeyPress, Sub(s, e)
                                               e.Cancel = True
                                               stopping.Set()
                                           End Sub

        Console.WriteLine($"CPU load on {port} every {interval.TotalSeconds}s, Ctrl+C to stop")
        Dim needSetup = True
        ' Load is the change of the counters between two reads, so the first frame comes after one interval
        Dim previous = ReadCpuTimes()
        Console.WriteLine($"Found {previous.Count} CPU cores" &
                          If(previous.Count > W, $", averaged into {W} columns", ""))
        Do Until stopping.Wait(interval)
            Dim current = ReadCpuTimes()
            Dim loads = CoreLoads(previous, current)
            previous = current
            Try
                ' Resent after any failure: a replugged device boots with whatever program is in flash
                If needSetup Then sender.Send(SetupBytes())
                needSetup = False
                Dim image = Render(loads, brightness, mirror)
                If flip Then image = Rotate180(image)
                sender.Send(DisplayProtocol.PixelBytes(image))
            Catch ex As Exception
                Console.Error.WriteLine(ex.Message)
                needSetup = True
            End Try
        Loop

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

    ' Per-core time counters keyed by core number, in units that only matter as a ratio
    Function ReadCpuTimes() As SortedDictionary(Of Integer, (Busy As Long, Total As Long))
        If RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then Return ReadCpuTimesWindows()
        Return ReadCpuTimesLinux()
    End Function

    ' Jiffy counters from /proc/stat; offline cores have no line and are skipped
    Function ReadCpuTimesLinux() As SortedDictionary(Of Integer, (Busy As Long, Total As Long))
        Dim times As New SortedDictionary(Of Integer, (Busy As Long, Total As Long))
        For Each line In File.ReadLines("/proc/stat")
            ' "cpu3 user nice system idle iowait irq softirq steal guest guest_nice"; the summary line is "cpu "
            If Not line.StartsWith("cpu") OrElse Not Char.IsDigit(line(3)) Then Continue For
            Dim parts = line.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
            Dim total As Long = 0
            For i = 1 To 8 ' guest time is already part of user, so it is not added again
                total += Long.Parse(parts(i))
            Next
            Dim idle = Long.Parse(parts(4)) + Long.Parse(parts(5)) ' idle + iowait
            times(Integer.Parse(parts(0).Substring(3))) = (total - idle, total)
        Next
        Return times
    End Function

    ' SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION, times in 100 ns units
    <StructLayout(LayoutKind.Sequential)>
    Structure ProcessorTimes
        Public IdleTime As Long
        Public KernelTime As Long ' includes IdleTime
        Public UserTime As Long
        Public DpcTime As Long
        Public InterruptTime As Long
        Public InterruptCount As UInteger
    End Structure

    <DllImport("ntdll.dll")>
    Function NtQuerySystemInformation(informationClass As Integer, <Out> buffer As ProcessorTimes(),
                                      length As Integer, ByRef returnLength As Integer) As Integer
    End Function

    ' Covers the processor group of this process, which is all cores up to 64 of them
    Function ReadCpuTimesWindows() As SortedDictionary(Of Integer, (Busy As Long, Total As Long))
        Const SystemProcessorPerformanceInformation As Integer = 8
        Dim times As New SortedDictionary(Of Integer, (Busy As Long, Total As Long))
        Dim buffer(Environment.ProcessorCount - 1) As ProcessorTimes
        Dim size = Marshal.SizeOf(Of ProcessorTimes)()
        Dim returned = 0
        Dim status = NtQuerySystemInformation(SystemProcessorPerformanceInformation, buffer, buffer.Length * size, returned)
        If status <> 0 Then Throw New IOException($"NtQuerySystemInformation failed: 0x{status:X8}")
        For i = 0 To returned \ size - 1
            Dim total = buffer(i).KernelTime + buffer(i).UserTime
            times(i) = (total - buffer(i).IdleTime, total)
        Next
        Return times
    End Function

    Function CoreLoads(previous As SortedDictionary(Of Integer, (Busy As Long, Total As Long)),
                       current As SortedDictionary(Of Integer, (Busy As Long, Total As Long))) As List(Of Double)
        Dim loads As New List(Of Double)
        For Each core In current
            Dim before As (Busy As Long, Total As Long)
            If Not previous.TryGetValue(core.Key, before) Then Continue For ' just came online
            Dim total = core.Value.Total - before.Total
            loads.Add(If(total > 0, (core.Value.Busy - before.Busy) / total, 0))
        Next
        Return loads
    End Function

    Function Render(loads As List(Of Double), brightness As Integer, mirror As Boolean) As Color(,)
        Dim image(W - 1, H - 1) As Color
        If loads.Count = 0 Then Return image
        ' More cores than columns: neighbouring cores share a column and are averaged
        Dim columns = Math.Min(loads.Count, W)
        Dim barWidth = W \ columns
        Dim gap = If(barWidth >= 3, 1, 0)
        Dim offset = (W - columns * barWidth) \ 2 ' spare columns split between both edges
        For c = 0 To columns - 1
            Dim first = c * loads.Count \ columns
            Dim last = (c + 1) * loads.Count \ columns - 1
            Dim load = loads.Skip(first).Take(last - first + 1).Average()
            Dim level = load * H
            Dim color = LoadColor(load)
            For y = 0 To H - 1 ' from the bottom
                ' The top pixel lights partially; PWM is linear in light, so coverage maps directly
                Dim coverage = Math.Min(1.0, Math.Max(0.0, level - y))
                Dim pixel = Scale(color, coverage * brightness / 100)
                For dx = 0 To barWidth - gap - 1
                    Dim x = offset + c * barWidth + dx
                    image(If(mirror, W - 1 - x, x), H - 1 - y) = pixel
                Next
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

    ' Hue goes green -> yellow -> red and value rises with load: dark green when idle, bright red at full
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
