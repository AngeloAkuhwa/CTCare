namespace CTCare.Shared.Utilities;

public static class SequentialGuid
{
    public static Guid NewGuid()
    {
        var guidArray = Guid.NewGuid().ToByteArray();

        var now = DateTime.UtcNow.Ticks;

        var timestampBytes = BitConverter.GetBytes(now);

        // Copy last 6 bytes of ticks into last 6 bytes of Guid
        // Big-endian => natural sort works
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timestampBytes);
        }

        Buffer.BlockCopy(timestampBytes, timestampBytes.Length - 6, guidArray, guidArray.Length - 6, 6);

        return new Guid(guidArray);
    }
}
