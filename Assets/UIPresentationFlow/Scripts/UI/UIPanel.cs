using System;

public interface IUIPanel { }

public abstract class UIPanel<TRefs> : UIBase<TRefs>, IUIPanel
    where TRefs : struct, Enum
{
}