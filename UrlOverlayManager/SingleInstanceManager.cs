using System.IO.Pipes;
using System.Text;

namespace UrlOverlayManager
{
    internal static class SingleInstanceManager
    {
        public const string MutexName = @"Local\UrlOverlayManager.SingleInstance";

        private const string PipeName = "UrlOverlayManager.SingleInstance";
        private const string ShowCommand = "show";
        private static readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        public static void StartServer(Action showMainForm)
        {
            SynchronizationContext? synchronizationContext = SynchronizationContext.Current;

            Task.Run(async () =>
            {
                while (!cancellation.IsCancellationRequested)
                {
                    try
                    {
                        using NamedPipeServerStream server = new NamedPipeServerStream(
                            PipeName,
                            PipeDirection.In,
                            1,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);

                        await server.WaitForConnectionAsync(cancellation.Token);

                        using StreamReader reader = new StreamReader(server, Encoding.UTF8);
                        string? command = await reader.ReadLineAsync();

                        if (string.Equals(command, ShowCommand, StringComparison.OrdinalIgnoreCase))
                        {
                            if (synchronizationContext != null)
                            {
                                synchronizationContext.Post(_ => showMainForm(), null);
                            }
                            else
                            {
                                showMainForm();
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                    }
                }
            }, cancellation.Token);
        }

        public static void StopServer()
        {
            cancellation.Cancel();
        }

        public static void NotifyExistingInstance()
        {
            try
            {
                using NamedPipeClientStream client = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.Out);

                client.Connect(500);

                using StreamWriter writer = new StreamWriter(client, Encoding.UTF8);
                writer.WriteLine(ShowCommand);
            }
            catch
            {
            }
        }
    }
}
