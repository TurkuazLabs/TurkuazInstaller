// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/services/OrderedProgress.cs
// 📌 Amac: UI progress olaylarini rapor sirasi korunarak uygun SynchronizationContext uzerinde calistirir
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Asenkron progress callback'lerinin terminal UI state'ini geriye sarmasini engeller ve kuyrugun drain edilmesini saglar
//
// Bagimli Oldugu Katman: Service | View

namespace TurkuazInstaller.Presentation.Services;

internal sealed class OrderedProgress<T> : IProgress<T>
{
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly Action<T> _handler;
    private readonly object _gate = new();
    private Task _tail = Task.CompletedTask;

    public OrderedProgress(
        Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handler = handler;
        _synchronizationContext =
            SynchronizationContext.Current;
    }

    public void Report(T value)
    {
        lock (_gate)
        {
            _tail = _tail
                .ContinueWith(
                    _ => DispatchAsync(value),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default)
                .Unwrap();
        }
    }

    public Task DrainAsync()
    {
        lock (_gate)
        {
            return _tail;
        }
    }

    private Task DispatchAsync(T value)
    {
        if (_synchronizationContext is null)
        {
            _handler(value);
            return Task.CompletedTask;
        }

        var completion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        _synchronizationContext.Post(
            _ =>
            {
                try
                {
                    _handler(value);
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            },
            null);

        return completion.Task;
    }
}
