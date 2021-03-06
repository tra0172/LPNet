'LongPollNet v1.0
'
'Requires NetCore v1.1
'https://github.com/amtra5/NetCore/releases/download/v1.1/NetCore.v1.1.zip
'
'---------------
'
'About:
'
'ChatServer: A simple HTTP long polling server
'LPClient: A class created by ChatServer that represents a single client
'
'---------------
'
'ChatServer:
'Usage-
'Dim server = New ChatServer()
'server.StartServer(port) - Starts a HTTP server on port, default 80
'server.StopServer() - Stops the server
'server.tblCLients - A hash table of current LPClient objects by GUID
'
'Sending Messages-
''client is an instance of LPClient created by ChatServer
'client.ID - Returns the client GUID
'client.SendMessage(ByVal data As String) - Send a message to the client
'
'Events-
'evtConnected(ByVal client As LPClient) - Triggered when a new client requests a GUID
'evtDisconnected(ByVal client As LPClient) - Triggered when a client signals it is disconnecting
'evtReceived(ByVal client As LPClient, ByVal data As String) - Triggered when a client sends a message to the server
'
'Setting up your client-
'1. Request a GUID by making a HTTP request to http://<server IP>/new. Your GUID will be returned in the message body.
'2. Sending a message to the server can be done by making a HTTP request to http://<server IP>/send/<your GUID>/<your message>. If it succeded, your message will be echoed back to you.
'3. Waiting for a message from the server can be done by making a HTTP request to http://<server IP>/poll/<your GUID>. The server will respond when a message is ready for you. It is recommended to make another polling request as soon as the first one is completed.
'4. To manually end your session, make a HTTP request to http://<server IP>/disconnect/<your GUID>. This is good practice, as it tells the server that you have quit.

Public Class LPNet
    Public Event evtConnected(ByVal lpcClient As LPClient)
    Public Event evtDisconnected(ByVal lpcClient As LPClient)
    Public Event evtReceived(ByVal lpcClient As LPClient, ByVal strData As String)

    Public tblClients As New Hashtable()
    Private nsvServer As NetServer

    Public Sub StartServer(Optional ByVal intPort As Int32 = 80)
        nsvServer = New NetServer()
        AddHandler nsvServer.evtReceived, AddressOf OnReceived
        nsvServer.StartListener(intPort)
    End Sub

    Public Sub StopServer()
        nsvServer.StopListener()
    End Sub

    Private Sub OnReceived(ByVal nctClient As NetClientObj, ByVal strData As String)
        Dim gidClient As Guid

        Dim tblParams = New Hashtable()
        Dim tblLines = Split(strData, vbCrLf)
        For Each strLine As String In tblLines
            Dim tblKeyValue = Split(strLine, ": ")
            If tblKeyValue.Length = 1 Then
                Dim command = Split(Split(strLine)(1).Substring(1), "/")
                If command(0) = "new" Then
                    'New connection, create a GUID
                    gidClient = Guid.NewGuid
                ElseIf command(0) = "send" Then
                    'Client wants to tell server a message
                    If tblClients.ContainsKey(command(1)) = True Then
                        tblClients(command(1).ToString).nctConnection = nctClient
                        RaiseEvent evtReceived(tblClients(command(1).ToString), command(2))
                        tblClients(command(1).ToString).SendMessage(command(2))
                    Else
                        Dim lpcThrowaway = New LPClient(nctClient, Guid.NewGuid)
                        lpcThrowaway.SendMessage("GUID Not Found")
                    End If
                ElseIf command(0) = "poll" Then
                    'Has previous GUID
                    gidClient = Guid.Parse(command(1))
                    If tblClients.ContainsKey(command(1)) = True Then
                        tblClients(command(1)).nctConnection = nctClient
                        If tblClients(command(1)).strBuffer <> "" Then
                            'If there is a message buffer, clear it
                            tblClients(command(1)).nctConnection.Send("HTTP/1.1 200 OK" & vbCrLf & "Content-Type: text/plain" & vbCrLf & "Content-Length: " & (tblClients(command(1)).strBuffer.Length + 1).ToString & vbCrLf & "Connection: close" & vbCrLf & vbCrLf & tblClients(command(1)).strBuffer)
                            tblClients(command(1)).strBuffer = ""
                            tblClients(command(1)).nctConnection = Nothing
                        End If
                    Else
                        Dim lpcThrowaway = New LPClient(nctClient, Guid.NewGuid)
                        lpcThrowaway.SendMessage("GUID Not Found")
                    End If
                ElseIf command(0) = "disconnect" Then
                    RaiseEvent evtDisconnected(tblClients(command(1).ToString))
                    tblClients.Remove(command(1))
                End If
            Else
                tblParams.Add(tblKeyValue(0), tblKeyValue(1))
            End If
        Next

        If tblClients.ContainsKey(gidClient.ToString) = True Then
            tblClients(gidClient.ToString).nctConnection = nctClient
        Else
            Dim lpcNewClient = New LPClient(nctClient, gidClient)
            tblClients.Add(gidClient.ToString, lpcNewClient)
            lpcNewClient.SendMessage(gidClient.ToString)
            RaiseEvent evtConnected(lpcNewClient)
        End If
    End Sub
End Class

Public Class LPClient
    Private gidClient As Guid
    Private strBuffer As String = ""
    Public nctConnection As NetClientObj

    Public Sub New(ByVal nctClient As NetClientObj, ByVal gidGUID As Guid)
        nctConnection = nctClient
        gidClient = gidGUID
    End Sub

    Public Sub SendMessage(ByVal strData As String)
        If IsNothing(nctConnection) Then
            strBuffer = strBuffer & strData & vbCrLf
        Else
            strBuffer = strBuffer & strData
            nctConnection.Send("HTTP/1.1 200 OK" & vbCrLf & "Content-Type: text/plain" & vbCrLf & "Content-Length: " & (strBuffer.Length + 1).ToString & vbCrLf & "Connection: close" & vbCrLf & vbCrLf & strBuffer & vbNewLine)
            strBuffer = ""
            nctConnection = Nothing
        End If
    End Sub

    Public ReadOnly Property ID() As String
        Get
            Return gidClient.ToString
        End Get
    End Property
End Class
