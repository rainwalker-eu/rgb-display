Imports System.Runtime.InteropServices
Imports Microsoft.Win32

Public Class DisplayPorts
    ' ST's generic virtual COM port ID, shared by many boards: the display has no ID of its own
    Private Const UsbId As String = "VID_0483&PID_5740"

    ' Serial port used when none is given, Nothing when it cannot be told.
    ' Linux keeps the first CDC ACM device; on Windows COM numbers are arbitrary, so the port is
    ' looked up by USB ID among the ports that are present now.
    Public Shared Function DefaultPort() As String
        If Not RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then Return "/dev/ttyACM0"
        Return FindWindowsPorts().FirstOrDefault()
    End Function

    ' Ports to offer in a port list: everything the system reports plus, on Linux, the per-display
    ' symlinks the udev rule from app/linux creates
    Public Shared Function All() As List(Of String)
        Dim ports = IO.Ports.SerialPort.GetPortNames().ToList()
        If IO.Directory.Exists("/dev/rgb3210") Then ports.InsertRange(0, IO.Directory.GetFiles("/dev/rgb3210"))
        Return ports
    End Function

    ' The registry also remembers displays that are unplugged, hence the check against live ports
    Private Shared Function FindWindowsPorts() As List(Of String)
        Dim found As New List(Of String)
        Dim present = IO.Ports.SerialPort.GetPortNames()
        Using devices = Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\USB\" & UsbId)
            If devices Is Nothing Then Return found
            For Each serial In devices.GetSubKeyNames()
                Using parameters = devices.OpenSubKey(serial & "\Device Parameters")
                    Dim name = TryCast(parameters?.GetValue("PortName"), String)
                    If name IsNot Nothing AndAlso present.Contains(name, StringComparer.OrdinalIgnoreCase) Then found.Add(name)
                End Using
            Next
        End Using
        Return found
    End Function
End Class
