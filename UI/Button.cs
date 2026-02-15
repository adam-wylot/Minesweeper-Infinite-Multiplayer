using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace SaperMultiplayer.UI;

#nullable enable
public class Button
{
    private static readonly Color _defColor = Color.Gray;

    private Action _onClick;

    private string _text;
    private bool _centered;
    private GraphicsDeviceManager? _graphics;
    private Rectangle _bounds;
    private Color _color;
    private Color _inBoundColor;
    private Color _drawColor;

    public bool DrawBoundry { get; set; } = false;
    public Color BorderColor { get; set; } = Color.Black;
    public int ThicknessOfBorder { get; set; } = 2;


    public Color ColorP {
        get
        {
            return _color;
        }
        set
        { 
            _color = value;
            CalculateInBoundColor();
        }
    }

    public Button(string text, Rectangle bounds, Action onClick, bool centered = false, GraphicsDeviceManager? graphics = null)
    {
        _text = text;
        _onClick = onClick;
        _color = _defColor;
        _drawColor = _color;
        CalculateInBoundColor();
        _bounds = bounds;
        _centered = centered;
        _graphics = graphics;

        if (centered && graphics == null)
        {
            centered = false;
        }
    }

    public void Update(MouseState mouse, MouseState prevMouse)
    {
        if (_bounds.Contains(mouse.Position))
        {
            _drawColor = _inBoundColor;
            if (mouse.LeftButton == ButtonState.Released && prevMouse.LeftButton == ButtonState.Pressed)
            {
                _onClick?.Invoke();
            }
        }
        else
        {
            _drawColor = ColorP;
        }
    }

    public void Draw(SpriteBatch sb, SpriteFont font, Texture2D pixel)
    {
        if (_centered && _graphics != null)
        {
            int screenWidth = _graphics.GraphicsDevice.Viewport.Width;
            int centerX = screenWidth / 2 - _bounds.Width / 2;
            _bounds.X = centerX;
        }

        sb.Draw(pixel, _bounds, _drawColor);
        if (DrawBoundry)
        {
            DrawBorder(sb, pixel);
        }

        Vector2 size = font.MeasureString(_text);
        Vector2 pos = new Vector2(_bounds.Center.X - size.X / 2, _bounds.Center.Y - size.Y / 2);
        sb.DrawString(font, _text, pos, Color.White);
    }

    private void CalculateInBoundColor()
    {
        (byte r, byte g, byte b) = (_color.R, _color.G, _color.B);
        _inBoundColor = new Color((byte)Math.Max(r - 35, 0), (byte)Math.Max(g - 35, 0), (byte)Math.Max(b - 35, 0));
    }

    private void DrawBorder(SpriteBatch sb, Texture2D pixel)
    {
        // Up - Down - Left - Right
        sb.Draw(pixel, new Rectangle(_bounds.X, _bounds.Y, _bounds.Width, ThicknessOfBorder), BorderColor);
        sb.Draw(pixel, new Rectangle(_bounds.X, _bounds.Y + _bounds.Height - ThicknessOfBorder, _bounds.Width, ThicknessOfBorder), BorderColor);
        sb.Draw(pixel, new Rectangle(_bounds.X, _bounds.Y, ThicknessOfBorder, _bounds.Height), BorderColor);
        sb.Draw(pixel, new Rectangle(_bounds.X + _bounds.Width - ThicknessOfBorder, _bounds.Y, ThicknessOfBorder, _bounds.Height), BorderColor);
    }
}