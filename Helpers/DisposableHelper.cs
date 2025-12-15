using System;

namespace PDS.App.Library.AppUtilities.Helpers
{
    public class DisposableHelper : IDisposable
    {
        // To detect redundant calls
        private bool _disposedValue;

        ~DisposableHelper() => Dispose(false);

        // Public implementation of Dispose pattern callable by consumers.
        //Ref: https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {

                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }
    }
}
