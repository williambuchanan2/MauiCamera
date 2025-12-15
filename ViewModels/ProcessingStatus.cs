namespace MauiCamera.ViewModels
{
    public interface ImageProcessingStatus : ProcessingStatus<string>
    {
    }

    public interface ProcessingStatus<T> : ProcessingStatus
    {
        /// <summary>
        /// Unique id. This is used when image manipulation (ie. conversion, rotation, resizing) is being performed by background processes
        /// </summary>
        new T? ProcessingId { get; set; }
    }

    public interface ProcessingStatus
    {
        /// <summary>
        /// Indicates if required processing has been performed 
        /// </summary>
        bool ProcessingDone { get; }
        /// <summary>
        /// Unique id. This is used when background processing is in progress
        /// </summary>
        object? ProcessingId { get; set; }
    }
}