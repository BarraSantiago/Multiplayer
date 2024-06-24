namespace DLL;

public class NonDisposableMessage
{
    public byte[] Message { get; set; }
    public bool ConfirmationReceived { get; set; }
    public DateTime LastSent { get; set; }

    public void ResendIfNotConfirmed()
    {
        if (!ConfirmationReceived && (DateTime.Now - LastSent).TotalSeconds > 5)
        {
            // Resend the message
            // You'll need to implement the actual sending of the message
            LastSent = DateTime.Now;
        }
    }
}