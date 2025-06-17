using System;

public class ImageButton : Button
{
    Pixmap pixmap;
    public Bitmap Texture
    {
        get => null;
        set
        {
            pixmap = new Pixmap( value.Size );
            pixmap.UpdateFromPixels( value.GetPixels().SelectMany( c =>
            {
                return new[]{(byte)(c.r * 255),
                        (byte)(c.g * 255),
                        ( byte )( c.b * 255 ),
                        ( byte )( c.a * 255 )};
            } ).ToArray(), (int)value.Size.x, (int)value.Size.y, ImageFormat.RGBA8888 );
        }
    }
    protected override void OnPaint()
    {
        base.OnPaint();
        var minSize = Math.Min( LocalRect.Width, LocalRect.Height );
        var widthDelta = (LocalRect.Width - minSize) / 2f;
        var heightDelta = (LocalRect.Height - minSize) / 2f;
        var boxRect = new Rect( LocalRect.TopLeft + new Vector2( widthDelta, heightDelta ), new Vector2( minSize ) );
        Paint.Draw( boxRect, pixmap );
    }
}