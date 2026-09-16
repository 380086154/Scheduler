namespace Scheduler.Core.Apis.Holiday.Dtos;

public class Response<T>
{
    public int Code { get; set; }

    public T? Holiday { get; set; }
}
