#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.ResourceSystem;
using HoyoToon.Editor.UI.Windows;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.ResourcesUI
{
    internal sealed class ResourceOperationProgress : IDisposable
    {
        private readonly CancellationTokenSource _cancellationSource;
        private bool _completed;

        internal ResourceOperationProgress(string title, string message, CancellationToken cancellationToken = default)
        {
            _cancellationSource = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : new CancellationTokenSource();

            ProgressDialog.Start(title, message, _cancellationSource.Cancel);
        }

        internal CancellationToken Token => _cancellationSource.Token;

        internal void Update(float progress, string message)
        {
            ProgressDialog.Update(progress, message);
        }

        internal void Complete(string completionMessage = null)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            ProgressDialog.End(completionMessage);
        }

        public void Dispose()
        {
            Complete();
            _cancellationSource.Dispose();
        }
    }

    internal static class ResourceOperationRunner
    {
        internal static void RunCancelableResourceOperation(string context, Func<CancellationToken, Task> operation)
        {
            AsyncUtil.RunFireAndForget(async () =>
            {
                using var cts = new CancellationTokenSource();
                try
                {
                    await operation(cts.Token);
                }
                catch (OperationCanceledException)
                {
                }
            }, context);
        }

        internal static ResourceOperationProgress CreateProgress(string title, string message, CancellationToken cancellationToken = default)
        {
            return new ResourceOperationProgress(title, message, cancellationToken);
        }

        internal static async Task ExecuteWithProgressAsync(
            string progressTitle,
            string progressMessage,
            Func<ResourceOperationProgress, Task> operation,
            string cancelledDialogMessage = null,
            string failureTitle = null,
            Func<Exception, string> failureMessageFactory = null,
            string failureLogMessage = null,
            bool rethrowOnCancel = true,
            bool rethrowOnFailure = false,
            CancellationToken cancellationToken = default)
        {
            using var progress = CreateProgress(progressTitle, progressMessage, cancellationToken);

            try
            {
                progress.Token.ThrowIfCancellationRequested();
                await operation(progress);
                progress.Complete();
            }
            catch (OperationCanceledException)
            {
                progress.Complete();

                if (!string.IsNullOrEmpty(cancelledDialogMessage))
                {
                    DialogWindow.ShowInfo("Cancelled", cancelledDialogMessage);
                }

                if (rethrowOnCancel)
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                progress.Complete();

                if (!string.IsNullOrEmpty(failureLogMessage))
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, $"{failureLogMessage}: {ex.Message}");
                }

                if (!string.IsNullOrEmpty(failureTitle))
                {
                    var failureMessage = failureMessageFactory != null ? failureMessageFactory(ex) : ex.Message;
                    DialogWindow.ShowError(failureTitle, failureMessage);
                }

                if (rethrowOnFailure)
                {
                    throw;
                }
            }
        }
    }
}
#endif
