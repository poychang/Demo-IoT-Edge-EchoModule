namespace EchoModule
{
    using Microsoft.Azure.Devices.Client;
    using System;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class Program
    {
        private static int _counter;

        public static void Main(string[] args)
        {
            Init().Wait();

            // 持續運行，直到此模組應用程式被移除或取消
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// 當模組應用程式被移除或取消時，執行清理程序
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);

            return tcs.Task;
        }

        /// <summary>
        /// 初始化 ModuleClient 並設置回調函數 (PipeMessage) 以接收包含溫度信息的消息
        /// </summary>
        private static async Task Init()
        {
            // 建立連線至 Edge runtime 的客戶端
            var ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(TransportType.Amqp_Tcp_Only);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub 模組客戶端已初始化。");

            // 註冊回調函數，使模組接收到訊息後可以執行該函數功能
            await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);
        }

        /// <summary>
        /// 只要此模組從 EdgeHub 發送訊息，就會呼叫此方法
        /// 此方法只會將訊息印出來並傳遞給下一個接收端，資料不會做任何改變
        /// </summary>
        private static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            var counterValue = Interlocked.Increment(ref _counter);
            var moduleClient = userContext as ModuleClient;

            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain expected values");
            }

            var messageBytes = message.GetBytes();
            var messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
                var pipeMessage = new Message(messageBytes);
                foreach (var prop in message.Properties)
                {
                    pipeMessage.Properties.Add(prop.Key, prop.Value);
                }
                await moduleClient.SendEventAsync("output1", pipeMessage);
                Console.WriteLine("Received message sent");
            }

            return MessageResponse.Completed;
        }
    }
}
