namespace CTCare.Shared.Interfaces;

public class PagedResult<TItemType>
{
    public int ItemCount { get; set; }
    public int PageLength { get; set; }
    public int CurrentPage { get; set; }
    public int PageCount { get; set; }
    public IList<TItemType> Items { get; set; }
}
public class GroupedPagedResult<TItemType>
{
    public int ItemCount { get; set; }
    public int PageLength { get; set; }
    public int CurrentPage { get; set; }
    public int PageCount { get; set; }
    public IList<GroupedResult<TItemType>> Items { get; set; }
}

public class GroupedResult<TItemType>
{
    public string Key { get; set; }
    public List<TItemType> GroupItems { get; set; }
}

public interface IPagedRequest
{
    int Page { get; set; }
    int PageLength { get; set; }
}
