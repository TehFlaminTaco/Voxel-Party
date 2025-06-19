using System;

public static class EditorHelpers
{
    public static void Watch<T>( this Widget widget, Func<T> getValue, Action<T> onChange, Func<T, int> buildHash = null )
    {
        if ( widget == null ) return;

        var watcher = new VoxelBuilder.Watcher
        {
            Target = widget,
            GetValue = () => getValue(),
            onChange = o => onChange( (T)o )
        };
        if ( buildHash != null )
        {
            watcher.BuildHash = o => buildHash( (T)o );
        }

        watcher.LastHash = watcher.BuildHash( watcher.GetValue() );
        VoxelBuilder.RegisterWatcher( watcher );
    }
}