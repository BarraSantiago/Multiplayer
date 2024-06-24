namespace DLL;

public class SortableMessage
{
    public byte[] Message { get; set; }
    public int Id { get; set; }

    public bool ShouldProcess(int lastProcessedId)
    {
        return Id > lastProcessedId;
    }
}