using System;
using System.Net.Sockets;
using System.Net;

//マニュアルリセットイベントのインスタンスを生成
public ManualResetEvent allDone = new ManualResetEvent(false);

//TCP/IPの接続開始処理
public async Task<bool> StartListening(int port)
{
    // IPアドレスとポート番号を指定して、ローカルエンドポイントを設定
    IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);

    // TCP/IPのソケットを作成
    Socket TcpServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    try 
    {
        TcpServer.Bind(localEndPoint);  // TCP/IPのソケットをローカルエンドポイントにバインド
        TcpServer.Listen(10);           // 待ち受け開始

        await Task.Run(() 
	{
	    while (true)
            {
                // シグナルの状態をリセット
                allDone.Reset();

		// 非同期ソケットを開始して、接続をリッスンする
		Debug.WriteLine("接続待機中...");
		TcpServer.BeginAccept(new AsyncCallback(AcceptCallback), TcpServer);

		// シグナル状態になるまで待機  
		allDone.WaitOne();
	    }
	});
     }
    catch (Exception ex)
    {
	    Debug.WriteLine(ex.ToString());
    }

    return false;
}

public void AcceptCallback(IAsyncResult ar)
{
	// シグナル状態にし、メインスレッドの処理を続行する
	allDone.Set();

	// クライアント要求を処理するソケットを取得
	Socket TcpServer = (Socket)ar.AsyncState;
	Socket TcpClient = TcpServer.EndAccept(ar);

	// 端末からデータ受信を待ち受ける
	StateObject state = new StateObject();
	state.workSocket = TcpClient;
	TcpClient.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
}

public static void ReceiveCallback(IAsyncResult ar)
{
	var content = string.Empty;

	try
	{
		// 非同期オブジェクトからソケット情報を取得
		StateObject state = (StateObject)ar.AsyncState;
		Socket TcpClient = state.workSocket;

		// クライアントソケットからデータを読み取り
		int bytesRead = TcpClient.EndReceive(ar);

		if (bytesRead > 0)
		{
			// 受信したデータを蓄積
			state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

			// 蓄積データの終端タグを確認
			content = state.sb.ToString();
			if (content.IndexOf("<EOF>") > -1)
			{
				// 終了タグ<EOF>があれば、読み取り完了
				Debug.WriteLine(string.Format("クライアントから「{0}」を受信", content));

				// ASCIIコードをバイトデータに変換
				byte[] byteData = Encoding.ASCII.GetBytes("OK");

				// クライアントへデータの送信を開始
				TcpClient.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), TcpClient);
			}
			else
			{
				// 取得していないデータがあるので、受信再開
				TcpClient.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
			}
		}
	}
	catch (Exception ex)
	{
		Debug.WriteLine(ex.ToString());
	}
}


private static void SendCallback(IAsyncResult ar)
{
	try
	{
		// 非同期オブジェクトからソケット情報を取得
		Socket TcpClient = (Socket)ar.AsyncState;

		// クライアントへデータ送信完了
		int bytesSent = TcpClient.EndSend(ar);
		Debug.WriteLine("「OK」をクライアントへ送信");

		//ソケット通信を終了
		Debug.WriteLine("接続終了");
		TcpClient.Shutdown(SocketShutdown.Both);
		TcpClient.Close();
	}
	catch (Ecveption ex)
	{
		Debug.WriteLine(ex.ToString());
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


