using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace PvPAdventure.UI;

public class UISlider : UIElement
{
    public bool Enabled = true;
    public bool IsHeld;
    public float Ratio;
    public event Action<float> OnDrag;
    public event Action<float> OnRelease;

    public UISlider()
    {
        Width.Set(0, 1f);
        Height.Set(16, 0f);
    }

    public override void LeftMouseDown(UIMouseEvent evt)
    {
        base.LeftMouseDown(evt);
        if (!Enabled)
            return;

        if (evt.Target == this)
            IsHeld = true;
    }

    public override void MouseOver(UIMouseEvent evt)
    {
        base.MouseOver(evt);
        if (Enabled)
            SoundEngine.PlaySound(SoundID.MenuTick);
    }

    public override void LeftMouseUp(UIMouseEvent evt)
    {
        base.LeftMouseUp(evt);
        if (!Enabled)
        {
            IsHeld = false;
            return;
        }

        if (IsHeld) 
            OnRelease?.Invoke(Ratio);
        IsHeld = false;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!Enabled)
        {
            IsHeld = false;
            return;
        }

        if (IsHeld)
        {
            var dims = GetDimensions();
            float num = Main.MouseScreen.X - dims.X;
            float newRatio = MathHelper.Clamp(num / dims.Width, 0f, 1f);
            if (Math.Abs(newRatio - Ratio) > float.Epsilon)
            {
                Ratio = newRatio;
                OnDrag?.Invoke(Ratio);
            }
        }
    }

    protected override void DrawSelf(SpriteBatch sb)
    {
        Rectangle rect = GetDimensions().ToRectangle();
        Color drawColor = Enabled ? Color.White : Color.Gray * 0.65f;
        Color background = Enabled && (IsHeld || IsMouseHovering) ? Main.OurFavoriteColor : drawColor;
        Utils.DrawInvBG(sb, rect, background);
        Rectangle innerBarArea = rect;
        innerBarArea.Inflate(-4, -4);
        sb.Draw(TextureAssets.ColorBar.Value, innerBarArea, drawColor);
        Texture2D blip = TextureAssets.ColorSlider.Value;
        Vector2 blipOrigin = blip.Size() * 0.5f;
        Vector2 blipPosition = new(innerBarArea.X + Ratio * innerBarArea.Width, innerBarArea.Center.Y);
        sb.Draw(blip, blipPosition, null, drawColor, 0f, blipOrigin, 1f, SpriteEffects.None, 0f);
    }

}
