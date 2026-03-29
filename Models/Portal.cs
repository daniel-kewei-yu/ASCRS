// ============================================================================
// File:        Portal.cs
// Project:     Aperture Science Cursor Relocation Suite (ASCRS)
// Author:      Daniel Yu
// Date:        2026-03-27
// Description:
//   Represents a single portal on the screen. Each portal has a centre point,
//   dimensions (width/height), an optional image (which may be animated GIF),
//   and a fallback colour if the image is missing. The portal's bounding box
//   is used for collision detection with the cursor sprite, and its midline
//   (vertical centre) is used as the split line during teleportation.
// ============================================================================

using System;
using System.Drawing;

namespace ASCRS.Models
{
    /// <summary>
    /// Represents a portal – a rectangular area on the screen that can be
    /// drawn with an image or a solid colour, and used for portal traversal.
    /// </summary>
    public class Portal
    {
        // --------------------------------------------------------------------
        // Properties
        // --------------------------------------------------------------------

        /// <summary>
        /// The centre point of the portal (screen coordinates).
        /// </summary>
        public Point Center { get; set; }

        /// <summary>
        /// The width of the portal in pixels.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// The height of the portal in pixels.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// The image to draw for this portal. May be null (in which case the
        /// fallback colour is used). Supports GIF animation.
        /// </summary>
        public Image? Image { get; set; }

        /// <summary>
        /// The fallback colour used if Image is null.
        /// </summary>
        public Color Color { get; set; }

        // --------------------------------------------------------------------
        // Constructor
        // --------------------------------------------------------------------

        /// <summary>
        /// Creates a new portal at the specified centre with the given dimensions.
        /// </summary>
        /// <param name="center">Centre point of the portal (screen coordinates).</param>
        /// <param name="width">Width of the portal in pixels.</param>
        /// <param name="height">Height of the portal in pixels.</param>
        /// <param name="image">Optional image to draw (e.g., a PNG or GIF).</param>
        public Portal(Point center, int width, int height, Image? image)
        {
            Center = center;
            Width = width;
            Height = height;
            Image = image;
        }

        // --------------------------------------------------------------------
        // Computed property: bounding box
        // --------------------------------------------------------------------

        /// <summary>
        /// Gets the bounding rectangle of the portal (left, top, width, height)
        /// based on its centre and dimensions. This rectangle is used for
        /// collision detection (AABB).
        /// </summary>
        public Rectangle Bounds => new Rectangle(Center.X - Width / 2, Center.Y - Height / 2, Width, Height);

        // --------------------------------------------------------------------
        // Collision detection
        // --------------------------------------------------------------------

        /// <summary>
        /// Determines whether the given point (typically the cursor centre)
        /// lies inside the portal's bounding rectangle.
        /// </summary>
        /// <param name="point">The point to test (screen coordinates).</param>
        /// <returns>True if the point is inside the portal, false otherwise.</returns>
        public bool Contains(Point point)
        {
            return Bounds.Contains(point);
        }

        // --------------------------------------------------------------------
        // Drawing
        // --------------------------------------------------------------------

        /// <summary>
        /// Draws the portal on the given Graphics object.
        /// If an image is available, it is drawn (GIF animations are handled
        /// externally via ImageAnimator). Otherwise, a solid rectangle is
        /// drawn using the fallback colour.
        /// </summary>
        /// <param name="g">Graphics context (e.g., from OnPaint).</param>
        public void Draw(Graphics g)
        {
            if (Image != null)
            {
                // Draw the portal image, scaling it to the portal's bounds.
                // If the image is a GIF, the frame is already updated by the
                // animation timer, so this will draw the current frame.
                g.DrawImage(Image, Bounds);
            }
            else
            {
                // No image available: draw a solid rectangle.
                using (SolidBrush brush = new SolidBrush(Color))
                {
                    g.FillRectangle(brush, Bounds);
                }
            }
        }

        // --------------------------------------------------------------------
        // Helper: portal midline (X coordinate of the vertical centre)
        // --------------------------------------------------------------------

        /// <summary>
        /// Gets the X coordinate of the portal's vertical centre line.
        /// This is used as the split line during portal traversal.
        /// </summary>
        public int MidlineX => Center.X;
    }
}