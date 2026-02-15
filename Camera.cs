using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SaperMultiplayer;
public class Camera
{
    // Consts
    // zoom limits
    private const float MinZoom = 0.3f;
    private const float MaxZoom = 3.0f;


    // Variables
    private Viewport _viewport;
    public Vector2 Position { get; set; }
    public float Zoom { get; set; } = 1.0f;
    public float Rotation { get; set; } = 0.0f;


    // Constructor
    public Camera(Viewport viewport)
    {
        _viewport = viewport;
        Position = Vector2.Zero;
    }


    // Methods
    public void AdjustZoom(float amount, Vector2 screenPosition)
    {
        // World position under the cursor before zoom
        Vector2 worldBefore = ScreenToWorld(screenPosition);

        // Apply zoom with clamping
        Zoom += amount;
        if (Zoom < MinZoom)
        {
            Zoom = MinZoom;
        }
        else if (Zoom > MaxZoom)
        {
            Zoom = MaxZoom;
        }

        // Move camera so that the world point stays under the same screen position
        Vector2 worldAfter = ScreenToWorld(screenPosition);
        Position += worldBefore - worldAfter;
    }

    public void Move(Vector2 amount)
    {
        Position += amount;
    }

    public Matrix GetTransform()
    {
        return Matrix.CreateTranslation(new Vector3(-Position.X, -Position.Y, 0)) *
               Matrix.CreateRotationZ(Rotation) *
               Matrix.CreateScale(new Vector3(Zoom, Zoom, 1)) *
               Matrix.CreateTranslation(new Vector3(_viewport.Width * 0.5f, _viewport.Height * 0.5f, 0));
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        return Vector2.Transform(screenPosition, Matrix.Invert(GetTransform()));
    }

    public Vector2 WorldToScreen(Vector2 worldPosition)
    {
        return Vector2.Transform(worldPosition, GetTransform());
    }
}