using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;

namespace Prh.UnifiedKonverter.Ui
{
    /// <summary>
    /// Fenster laufen in einem eigenen STA-Thread, weil nicht garantiert ist,
    /// in welchem Thread TIA Portal den Menü-Aufruf des Add-Ins ausführt.
    /// Openness darf aber nur aus dem Thread des Menü-Aufrufs benutzt werden
    /// („Cross-thread operation is not valid in Openness within STA“). Deshalb wartet dieser
    /// Thread nicht nur auf das Fenster, sondern führt währenddessen alles aus, was das Fenster
    /// mit <see cref="Invoke{T}"/> schickt.
    /// </summary>
    internal static class UiThread
    {
        private static Thread _owner;
        private static BlockingCollection<Action> _queue;

        public static void Run(Action action)
        {
            Exception error = null;
            BlockingCollection<Action> queue = new BlockingCollection<Action>();
            Thread thread = new Thread(() =>
            {
                try
                {
                    Application.EnableVisualStyles();
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    queue.CompleteAdding();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;

            Thread previousOwner = _owner;
            BlockingCollection<Action> previousQueue = _queue;
            _owner = Thread.CurrentThread;
            _queue = queue;
            try
            {
                thread.Start();
                foreach (Action work in queue.GetConsumingEnumerable()) work();
                thread.Join();
            }
            finally
            {
                _owner = previousOwner;
                _queue = previousQueue;
                queue.Dispose();
            }
            if (error != null) throw new InvalidOperationException("Fehler im Fenster des Add-Ins.", error);
        }

        /// <summary>
        /// Führt <paramref name="func"/> im Thread des Menü-Aufrufs aus (für alle Openness-Zugriffe aus einem Fenster).
        /// Das Fenster wartet so lange; Ausnahmen kommen unverändert beim Aufrufer an.
        /// </summary>
        public static T Invoke<T>(Func<T> func)
        {
            BlockingCollection<Action> queue = _queue;
            if (queue == null || Thread.CurrentThread == _owner) return func();

            T result = default(T);
            ExceptionDispatchInfo error = null;
            using (ManualResetEvent done = new ManualResetEvent(false))
            {
                queue.Add(() =>
                {
                    try
                    {
                        result = func();
                    }
                    catch (Exception ex)
                    {
                        error = ExceptionDispatchInfo.Capture(ex);
                    }
                    finally
                    {
                        done.Set();
                    }
                });
                done.WaitOne();
            }
            if (error != null) error.Throw();
            return result;
        }

        public static void Invoke(Action action)
        {
            Invoke<object>(() =>
            {
                action();
                return null;
            });
        }
    }
}
