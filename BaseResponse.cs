namespace MauiCamera
{

    /// <summary>
    /// Standard response for API calls to contain error status and information
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BaseResponse<T>: BaseResponseCommon
    {
        public new T Data { get; set; }
    }
}
