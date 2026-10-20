using System;

public interface IUIRoot { }

public abstract class UIRoot<TRefs> : UIBase<TRefs>, IUIRoot
    where TRefs : struct, Enum
{
}