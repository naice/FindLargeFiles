namespace FileSizeTree.Core
{
    public interface ISizeProvider
    {
        long GetSize(ElementType type, string path);
    }
}
