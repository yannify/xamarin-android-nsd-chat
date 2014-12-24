using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Net;
using Android.Util;
using Java.IO;
using Java.Lang;
using System.Collections.Concurrent;

namespace com.testy.chat.app
{
    public class ChatConnection 
    {
        private Handler mUpdateHandler;
        private ChatServer mChatServer;
        private ChatClient mChatClient;

        private const string TAG = "ChatConnection";

        private Socket mSocket;
        private int mPort = -1;

        public ChatConnection(Handler handler) {
            mUpdateHandler = handler;
            mChatServer = new ChatServer(handler, this);
        }

        public void TearDown() {
            mChatServer.TearDown();
            mChatClient.TearDown();
        }

        public void ConnectToServer(InetAddress address, int port) {
            mChatClient = new ChatClient(address, port, this);
        }

        public void SendMessage(string msg) {
            if (mChatClient != null) {
                mChatClient.SendMessage(msg);
            }
        }
    
        public int GetLocalPort() {
            return mPort;
        }
    
        public void SetLocalPort(int port) {
            mPort = port;
        }
    
        private readonly object messageLock = new object();
        public void UpdateMessages(string msg, bool local) 
        {
            lock(messageLock)
            {
                 Log.Error(TAG, "Updating message: " + msg);

                if (local) {
                    msg = "me: " + msg;
                } else {
                    msg = "them: " + msg;
                }

                Bundle messageBundle = new Bundle();
                messageBundle.PutString("msg", msg);

                Message message = new Message();
                message.Data = messageBundle;
                mUpdateHandler.SendMessage(message);
            }
        }

        private readonly object socketLock = new object();
        private void SetSocket(Socket socket) 
        {
            lock(socketLock)
            {
                Log.Debug(TAG, "setSocket being called.");
                if (socket == null) {
                    Log.Debug(TAG, "Setting a null socket.");
                }
                if (mSocket != null) {
                    if (mSocket.IsConnected) {
                        try {
                            mSocket.Close();
                        } catch (IOException e) {
                            // TODO(alexlucas): Auto-generated catch block
                            e.PrintStackTrace();
                        }
                    }
                }
                mSocket = socket;
            }
        }

        private Socket GetSocket() {
            return mSocket;
        }

        private class ChatServer {
            public ServerSocket mServerSocket = null;
            private ChatConnection mChatConnection;

            Thread mThread = null;

            public ChatServer(Handler handler, ChatConnection chatConnection) {
             
                mThread = new Thread(new ServerThread(chatConnection));
                mThread.Start();
            }

            public void TearDown() {
                mThread.Interrupt();
                try {
                    this.mChatConnection.mChatServer.mServerSocket.Close();
                } catch (IOException ioe) {
                    Log.Error(TAG, "Error when closing server socket.");
                }
            }

            private class ServerThread: Java.Lang.Object, IRunnable
            {
                private ChatConnection mChatConnection;
                
                public ServerThread( ChatConnection chatConnection)
                {
                    this.mChatConnection = chatConnection;
                }
                public void Run() {

                    try {
                        // Since discovery will happen via Nsd, we don't need to care which port is
                        // used.  Just grab an available one  and advertise it via Nsd.
                        this.mChatConnection.mChatServer.mServerSocket = new ServerSocket(0);
                        this.mChatConnection.SetLocalPort(this.mChatConnection.mChatServer.mServerSocket.LocalPort);
                    
                        while (!Thread.CurrentThread().IsInterrupted) {
                            Log.Debug(TAG, "ServerSocket Created, awaiting connection");
                            this.mChatConnection.SetSocket(this.mChatConnection.mChatServer.mServerSocket.Accept());
                            Log.Debug(TAG, "Connected.");
                            if (this.mChatConnection.mChatClient == null) {
                                int port = this.mChatConnection.mSocket.Port;
                                InetAddress address = this.mChatConnection.mSocket.InetAddress;
                                this.mChatConnection.ConnectToServer(address, port);
                            }
                        }
                    } catch (IOException e) {
                        Log.Error(TAG, "Error creating ServerSocket: ", e);
                        e.PrintStackTrace();
                    }
                }
            }
        }

        private class ChatClient {

            private InetAddress mAddress;
            private int PORT;

            private const string CLIENT_TAG = "ChatClient";

            private Thread mSendThread;
            private Thread mRecThread;

            private ChatConnection mChatConnection;

            public ChatClient(InetAddress address, int port, ChatConnection chatConnection) {

                Log.Debug(CLIENT_TAG, "Creating chatClient");
                this.mAddress = address;
                this.PORT = port;

                this.mChatConnection = chatConnection;

                this.mSendThread = new Thread(new SendingThread(chatConnection));
                this.mSendThread.Start();
            }

            private class SendingThread : Java.Lang.Object, IRunnable 
            {
                private BlockingCollection<string> mMessageQueue;
                private int QUEUE_CAPACITY = 10;
                private ChatConnection mChatConnection;
                
                public SendingThread(ChatConnection chatConnection)
                {
                    this.mChatConnection = chatConnection;
                    this.mMessageQueue = new BlockingCollection<string>(QUEUE_CAPACITY);
                }

                public void Run() {
                    try {
                        if (this.mChatConnection.GetSocket() == null) {
                            this.mChatConnection.SetSocket(new Socket(this.mChatConnection.mChatClient.mAddress, this.mChatConnection.mChatClient.PORT));
                            Log.Debug(CLIENT_TAG, "Client-side socket initialized.");

                        } else {
                            Log.Debug(CLIENT_TAG, "Socket already initialized. skipping!");
                        }

                        this.mChatConnection.mChatClient.mRecThread = new Thread(new ReceivingThread(this.mChatConnection));
                        this.mChatConnection.mChatClient.mRecThread.Start();

                    } catch (UnknownHostException e) {
                        Log.Debug(CLIENT_TAG, "Initializing socket failed, UHE", e);
                    } catch (IOException e) {
                        Log.Debug(CLIENT_TAG, "Initializing socket failed, IOE.", e);
                    }

                    while (true) {
                        try {
                            string msg = mMessageQueue.Take();
                            this.mChatConnection.SendMessage(msg);
                        } catch (InterruptedException ie) {
                            Log.Debug(CLIENT_TAG, "Message sending loop interrupted, exiting");
                        }
                    }
                }
            }

            private class ReceivingThread : Java.Lang.Object, IRunnable 
            {
                private ChatConnection mChatConnection;
                public ReceivingThread(ChatConnection chatConnection)
                {
                    this.mChatConnection = chatConnection;
                }
                public void Run() {
                    BufferedReader input;
                    try {
                        input = new BufferedReader(new InputStreamReader(
                                this.mChatConnection.mSocket.InputStream));
                        while (!Thread.CurrentThread().IsInterrupted) {

                            string messageStr = null;
                            messageStr = input.ReadLine();
                            if (messageStr != null) {
                                Log.Debug(CLIENT_TAG, "Read from the stream: " + messageStr);
                                this.mChatConnection.UpdateMessages(messageStr, false);
                            } else {
                                Log.Debug(CLIENT_TAG, "The nulls! The nulls!");
                                break;
                            }
                        }
                        input.Close();

                    } catch (IOException e) {
                        Log.Error(CLIENT_TAG, "Server loop error: ", e);
                    }
                }
            }

            public void TearDown() {
                try {
                    this.mChatConnection.GetSocket().Close();
                } catch (IOException ioe) {
                    Log.Error(CLIENT_TAG, "Error when closing server socket.");
                }
            }

            public void SendMessage(string msg) {
                try {
                    Socket socket = this.mChatConnection.GetSocket();
                    if (socket == null) {
                        Log.Debug(CLIENT_TAG, "Socket is null, wtf?");
                    } else if (socket.OutputStream == null) {
                        Log.Debug(CLIENT_TAG, "Socket output stream is null, wtf?");
                    }

                    PrintWriter printWriterOut = new PrintWriter(
                            new BufferedWriter(
                                    new OutputStreamWriter(this.mChatConnection.GetSocket().OutputStream)), true);
                    printWriterOut.Println(msg);
                    printWriterOut.Flush();
                    this.mChatConnection.UpdateMessages(msg, true);
                } catch (UnknownHostException e) {
                   // Log.Debug(CLIENT_TAG, "Unknown Host", e);
                } catch (IOException e) {
                   // Log.Debug(CLIENT_TAG, "I/O Exception", e);
                } catch (Java.Lang.Exception e) {
                   // Log.Debug(CLIENT_TAG, "Error3", e);
                }
                // Log.Debug(CLIENT_TAG, "Client sent message: " + msg);
            }
        }
    }
}
