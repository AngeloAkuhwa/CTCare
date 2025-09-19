namespace CTCare.Shared.Utilities;

public enum SequentialGuidType
{
    SequentialAsString,
    SequentialAsBinary,
    SequentialAtEnd
}
public static class SequentialGuid
{
    public static Guid NewGuid(SequentialGuidType guidType = SequentialGuidType.SequentialAsString)
    {
        var guidArray = Guid.NewGuid().ToByteArray();
        var timeStamp = DateTime.UtcNow.Ticks / 10000L;
        var timeStampBytes = BitConverter.GetBytes(timeStamp);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timeStampBytes);
        }

        var guidBytes = new byte[16];
        switch (guidType)
        {
            case SequentialGuidType.SequentialAsString:
            case SequentialGuidType.SequentialAsBinary:
                Buffer.BlockCopy(timeStampBytes, 2, guidArray, 0, 6);
                Buffer.BlockCopy(guidArray, 0, guidArray, 6, 10);
                if (guidType == SequentialGuidType.SequentialAsString && BitConverter.IsLittleEndian)
                {
                    Array.Reverse(guidBytes, 0, 4);
                    Array.Reverse(guidBytes, 2, 4);
                }
                break;
            case SequentialGuidType.SequentialAtEnd:
                Buffer.BlockCopy(guidArray, 0, guidArray, 0, 10);
                Buffer.BlockCopy(timeStampBytes, 2, guidArray, 10, 6);
                break;
            default:
                throw new Exception($"Case Missing for {guidType}");


        }

        return new Guid(guidArray);
    }
}
