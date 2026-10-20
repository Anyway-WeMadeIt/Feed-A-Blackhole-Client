using System;

public interface IUIPage
{
}

public abstract class UIPage<TRefs> : UIBase<TRefs>, IUIPage
    where TRefs : struct, Enum
{
}