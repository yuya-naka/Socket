using System;
using System.Net.Sockets;
using System.Net;

// マニュアルリセットイベントのインスタンスを生成
private static ManualResetEvent connectDone = new ManualResetEvent(false);  //接続シグナル用
private static ManualResetEvent sendDone = new ManualResetEvent(false);     //送信シグナル用
private static ManualResetEvent receiveDone = new ManualResetEvent(false);  //受信シグナル用

// 受信データのレスポンス
private static string response = string.Empty;

public string StartClient(string ipaddress, int port, string data)
{
    response = string.Empty;

    //シグナルをリセット
    connectDone.Reset();
    sendDone.Reset();
    receiveDone.Reset();

    // サーバーへ接続
    try
    {
        // IPアドレスとポート番号を取得
        IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(ipaddress), port);

        // TCP/IPのソケットを作成
        Socket client = new Socket(IPAddress.Parse(ipaddress).AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        // エンドポイント（IPアドレスとポート）へ接続
        client.BeginConnect(endpoint, new AsyncCallback(ConnectCallback), client);
        connectDone.WaitOne();  //接続シグナルになるまで待機

        // ASCIIエンコーディングで送信データをバイトの配列に変換
        byte[] byteData = Encoding.ASCII.GetBytes(data + "<EOF>");

        // サーバーへデータを送信
        client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client);
        sendDone.WaitOne();  //送信シグナルになるまで待機

        // ソケット情報を保持する為のオブジェクトを生成
        StateObject state = new StateObject();
        state.workSocket = client;

        // サーバーからデータ受信
        client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
        receiveDone.WaitOne();  //受信シグナルになるまで待機

        // ソケット接続終了
        client.Shutdown(SocketShutdown.Both);
        client.Close();
        Debug.WriteLine("接続終了");
    }
    catch (Exception e)
    {
        Debug.WriteLine(e.ToString());
    }

    return response;
}

private static void ConnectCallback(IAsyncResult ar)
{
    try
    {
        // ソケットを取得
        Socket client = (Socket)ar.AsyncState;

        // 非同期接続を終了
        client.EndConnect(ar);
        Debug.WriteLine("接続完了");

        // シグナル状態にし、メインスレッドの処理を続行する
        connectDone.Set();
    }
    catch (Exception e)
    {
        Debug.WriteLine(e.ToString());
    }
}

private static void SendCallback(IAsyncResult ar)
{
    try
    {
        // ソケットを取得
        Socket client = (Socket)ar.AsyncState;

        // 非同期送信を終了
        int bytesSent = client.EndSend(ar);
        Debug.WriteLine("送信完了");

        // シグナル状態にし、メインスレッドの処理を続行する
        sendDone.Set();
    }
    catch (Exception e)
    {
        Debug.WriteLine(e.ToString());
    }
}

private static void ReceiveCallback(IAsyncResult ar)
{
    try
    {
        // ソケット情報を保持する為のオブジェクトから情報取得
        StateObject state = (StateObject)ar.AsyncState;
        Socket client = state.workSocket;

        // 非同期受信を終了
        int bytesRead = client.EndReceive(ar);

        if (bytesRead > 0)
        {
            // 受信したデータを蓄積
            state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

            // 受信処理再開（まだ受信しているデータがあるため）
            client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
        }
        else
        {
            // 受信完了
            if (state.sb.Length > 1)
            {
                response = state.sb.ToString();
                Debug.WriteLine("サーバーから「{0}」を受信", response);
            }
            // シグナル状態にし、メインスレッドの処理を続行する
            receiveDone.Set();
        }
    }
    catch (Exception e)
    {
        Debug.WriteLine(e.ToString());
    }
}

// 非同期処理でソケット情報を保持する為のオブジェクト
public class StateObject
{
    // 受信バッファサイズ
    public const int BufferSize = 1024;

    // 受信バッファ
    public byte[] buffer = new byte[BufferSize];

    // 受信データ
    public StringBuilder sb = new StringBuilder();

    // ソケット
    public Socket workSocket = null;
}
