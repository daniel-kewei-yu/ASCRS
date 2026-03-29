// ============================================================================
// File:        MainForm.cs
// Project:     Aperture Science Cursor Relocation Suite (ASCRS)
// Author:      Daniel Yu
// Date:        2026-03-27
// Description:
//   This is the main window of the application. It draws a transparent overlay
//   over the entire screen, displays two portal images (Blue and Orange), and
//   handles the interactive portal effect: the cursor splits at the portal's
//   midline, the part that passes through appears at the other portal, and
//   the system cursor teleports when the entire sprite has crossed. The window
//   also includes a draggable button to toggle between Edit Mode (drag portals)
//   and Play Mode (cursor teleport). The system cursor is hidden only while it
//   is inside a portal in Play Mode; otherwise it remains visible.
// ============================================================================

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ASCRS.Models;
using ASCRS.Utilities;

namespace ASCRS.Forms
{
    public partial class MainForm : Form
    {
        // --------------------------------------------------------------------
        // Portal objects
        // --------------------------------------------------------------------
        private Portal? bluePortal;   // The Blue portal (usually on the left)
        private Portal? orangePortal;  // The Orange portal (usually on the right)

        // --------------------------------------------------------------------
        // Mode flags
        // --------------------------------------------------------------------
        private bool editMode = true;  // True = Edit Mode (can drag portals),
                                      // False = Play Mode (cursor teleport)

        // --------------------------------------------------------------------
        // Timers
        // --------------------------------------------------------------------
        private Timer? positionTimer;  // Polls cursor position every 10 ms in Play Mode
        private Timer? gifTimer;       // Advances GIF animation frames independently

        // --------------------------------------------------------------------
        // Portal dragging (Edit Mode)
        // --------------------------------------------------------------------
        private Portal? draggingPortal = null;   // The portal currently being dragged
        private Point dragStartCursor;           // Screen cursor position at drag start
        private Point dragStartPortalCenter;     // Portal centre at drag start

        // --------------------------------------------------------------------
        // Custom cursor sprite
        // --------------------------------------------------------------------
        private Image? cursorSprite;             // The custom cursor image (PNG)
        private Point cursorOffset = new Point(5, 5);   // Offset to align sprite with hot spot
        private float cursorScale = 1.0f;                // Scaling factor to keep sprite size reasonable
        private const int CursorSize = 20;                // Maximum width/height of sprite in pixels
        private Point actualCursorPos;                    // Current cursor position (screen coordinates)
        private int spriteWidth;                          // Scaled sprite width
        private int spriteHeight;                         // Scaled sprite height
        private int hotspotX;                             // X coordinate of the sprite's hot spot (centre)
        private int hotspotY;                             // Y coordinate of the sprite's hot spot

        // --------------------------------------------------------------------
        // Portal crossing tracking
        // --------------------------------------------------------------------
        private bool isSplitting = false;          // True while the cursor is partially through a portal
        private Portal? entrancePortal = null;     // The portal the cursor is currently entering
        private Portal? exitPortal = null;         // The portal where the passed part appears
        private bool enteringFromRight = false;    // True if the cursor entered from the right side of the portal
        private bool insidePortal = false;         // True when the cursor sprite overlaps any portal (for cursor visibility)
        private bool justTeleported = false;       // One‑frame flag to prevent immediate split after teleport

        // --------------------------------------------------------------------
        // Local cursor hiding (transparent cursor)
        // --------------------------------------------------------------------
        private Cursor? transparentCursor;         // A 1×1 transparent cursor used to hide the system cursor

        // --------------------------------------------------------------------
        // UI elements
        // --------------------------------------------------------------------
        private Button modeToggleButton = null!;   // Button to toggle Edit/Play Mode (draggable)
        private bool isDraggingButton = false;     // True while the button is being dragged
        private Point dragButtonStart;             // Mouse offset within the button at drag start
        private bool buttonDragged = false;        // True if a drag occurred (prevents accidental click)

        // --------------------------------------------------------------------
        // Hotkey IDs
        // --------------------------------------------------------------------
        private const int HOTKEY_ID_EXIT = 2;      // Hotkey for Escape (exits application)

        // --------------------------------------------------------------------
        // Windows messages constants
        // --------------------------------------------------------------------
        private const int WM_HOTKEY = 0x0312;              // Message sent when a hotkey is pressed
        private const int WM_SYSCOMMAND = 0x0112;          // System command (e.g., minimize, restore)
        private const int SC_MINIMIZE = 0xF020;            // System command to minimize window
        private const int WM_LBUTTONDOWN = 0x0201;         // Left mouse button down
        private const int WM_LBUTTONUP = 0x0202;           // Left mouse button up
        private const int WM_RBUTTONDOWN = 0x0204;         // Right mouse button down
        private const int WM_RBUTTONUP = 0x0205;           // Right mouse button up
        private const int WM_MOUSEMOVE = 0x0200;           // Mouse movement
        private const int WM_LBUTTONDBLCLK = 0x0203;       // Left mouse button double click

        // Extended window style to ensure the window appears in the taskbar
        private const int WS_EX_APPWINDOW = 0x00040000;

        // ====================================================================
        // Constructor
        // ====================================================================
        public MainForm()
        {
            InitializeComponent();
            SetupForm();
            LoadCustomCursorSprite();
            LoadFormIcon();
            InitializePortals();
            SetupModeButton();
            RegisterHotkeys();
            SetupTimers();

            this.Cursor = Cursors.Default;
            CreateTransparentCursor();
            actualCursorPos = Cursor.Position;
        }

        // ====================================================================
        // Form setup
        // ====================================================================
        /// <summary>
        /// Configures the form to be borderless, topmost, transparent, and covering all monitors.
        /// </summary>
        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;          // Black becomes fully transparent
            this.ShowInTaskbar = true;                   // Show in taskbar (icon appears)
            this.Bounds = SystemInformation.VirtualScreen; // Cover all monitors

            // Add extended style to allow minimizing via taskbar icon
            int exStyle = NativeMethods.GetWindowLong(this.Handle, NativeMethods.GWL_EXSTYLE);
            exStyle |= WS_EX_APPWINDOW;
            NativeMethods.SetWindowLong(this.Handle, NativeMethods.GWL_EXSTYLE, exStyle);

            // Initially, do not make the window click‑through; we'll use manual forwarding in Play Mode.
            SetClickThrough(false);
        }

        // ====================================================================
        // Timer setup
        // ====================================================================
        /// <summary>
        /// Creates two timers:
        /// - positionTimer: polls the system cursor position every 10 ms in Play Mode.
        /// - gifTimer: advances GIF animation frames every 10 ms (always running).
        /// </summary>
        private void SetupTimers()
        {
            positionTimer = new Timer();
            positionTimer.Interval = 10;
            positionTimer.Tick += PositionTimer_Tick;
            positionTimer.Start();

            gifTimer = new Timer();
            gifTimer.Interval = 10;
            gifTimer.Tick += GifTimer_Tick;
            gifTimer.Start();
        }

        // ====================================================================
        // Cursor hiding helpers
        // ====================================================================
        /// <summary>
        /// Creates a 1×1 transparent cursor that we can assign to the form to hide the system cursor
        /// when it is over our window.
        /// </summary>
        private void CreateTransparentCursor()
        {
            using (Bitmap bmp = new Bitmap(1, 1))
                transparentCursor = new Cursor(bmp.GetHicon());
        }

        // ====================================================================
        // Load custom cursor sprite
        // ====================================================================
        /// <summary>
        /// Loads the cursor sprite from Resources/cursors/cursorSprite.png, scales it to a maximum
        /// dimension (CursorSize), and computes the hotspot (centre of the image).
        /// </summary>
        private void LoadCustomCursorSprite()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                "Resources", "cursors", "cursorSprite.png");
            try
            {
                if (File.Exists(path))
                {
                    cursorSprite = Image.FromFile(path);
                    // Determine scaling factor to fit within CursorSize.
                    int maxDim = Math.Max(cursorSprite.Width, cursorSprite.Height);
                    if (maxDim > CursorSize)
                        cursorScale = (float)CursorSize / maxDim;
                    spriteWidth = (int)(cursorSprite.Width * cursorScale);
                    spriteHeight = (int)(cursorSprite.Height * cursorScale);
                    // Hotspot is the centre of the sprite.
                    hotspotX = (int)(cursorSprite.Width / 2 * cursorScale);
                    hotspotY = (int)(cursorSprite.Height / 2 * cursorScale);
                }
            }
            catch { cursorSprite = null; }
        }

        // ====================================================================
        // Load application icon
        // ====================================================================
        /// <summary>
        /// Loads the application icon (ASCRSicon.ico) from the Resources/icons folder.
        /// The icon will be displayed in the taskbar and in the title bar (though the form is borderless).
        /// </summary>
        private void LoadFormIcon()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                       "Resources", "icons", "ASCRSicon.ico");
            try
            {
                if (File.Exists(path))
                    this.Icon = new Icon(path);
            }
            catch { }
        }

        // ====================================================================
        // Load portal images (supports GIF animation)
        // ====================================================================
        /// <summary>
        /// Loads the portal images (bluePortal.gif and orangePortal.gif) from Resources/icons.
        /// The images are loaded using Image.FromFile, and if they are GIFs, the animation is started
        /// via ImageAnimator.Animate. The portal bounds are determined by the image dimensions.
        /// </summary>
        private void InitializePortals()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string bluePath = Path.Combine(baseDir, "Resources", "icons", "bluePortal.gif");
            string orangePath = Path.Combine(baseDir, "Resources", "icons", "orangePortal.gif");

            Image? blueImage = null, orangeImage = null;
            try
            {
                if (File.Exists(bluePath))
                {
                    blueImage = Image.FromFile(bluePath);
                    // Start the GIF animation (the empty event handler is required by the method).
                    ImageAnimator.Animate(blueImage, (sender, e) => { });
                }
                if (File.Exists(orangePath))
                {
                    orangeImage = Image.FromFile(orangePath);
                    ImageAnimator.Animate(orangeImage, (sender, e) => { });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load portal images: {ex.Message}");
            }

            // If an image is missing, use a default size; otherwise use the actual image size.
            int width = 100, height = 100;
            if (blueImage != null) { width = blueImage.Width; height = blueImage.Height; }

            // Centre portals on the virtual screen (all monitors combined).
            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            int centerX = virtualScreen.Left + virtualScreen.Width / 2;
            int centerY = virtualScreen.Top + virtualScreen.Height / 2;

            // Place Blue portal 200 pixels left of centre, Orange portal 200 pixels right.
            bluePortal = new Portal(new Point(centerX - 200, centerY), width, height, blueImage);
            orangePortal = new Portal(new Point(centerX + 200, centerY), width, height, orangeImage);

            // Fallback colours if images are missing.
            if (blueImage == null) bluePortal.Color = Color.Blue;
            if (orangeImage == null) orangePortal.Color = Color.Orange;
        }

        // ====================================================================
        // GIF animation timer
        // ====================================================================
        /// <summary>
        /// Advances the frame of any GIF images (portals) and invalidates their bounds to trigger repaint.
        /// Called every 10 ms.
        /// </summary>
        private void GifTimer_Tick(object? sender, EventArgs e)
        {
            if (bluePortal?.Image != null)
            {
                ImageAnimator.UpdateFrames(bluePortal.Image);
                Invalidate(bluePortal.Bounds);
            }
            if (orangePortal?.Image != null)
            {
                ImageAnimator.UpdateFrames(orangePortal.Image);
                Invalidate(orangePortal.Bounds);
            }
        }

        // ====================================================================
        // Mode toggle button
        // ====================================================================
        /// <summary>
        /// Creates the draggable button that toggles between Edit Mode and Play Mode.
        /// The button has a visible border and a dark background; its text changes with the mode.
        /// </summary>
        private void SetupModeButton()
        {
            modeToggleButton = new Button
            {
                Text = "EDIT MODE",
                BackColor = Color.FromArgb(80, 80, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Size = new Size(100, 30),
                Location = new Point(10, 10),
                Cursor = Cursors.SizeAll            // Indicates the button can be dragged
            };
            // Add a visible border.
            modeToggleButton.FlatAppearance.BorderSize = 2;
            modeToggleButton.FlatAppearance.BorderColor = Color.LightGray;
            // Attach event handlers.
            modeToggleButton.Click += ModeButton_Click;
            modeToggleButton.MouseDown += ModeButton_MouseDown;
            modeToggleButton.MouseMove += ModeButton_MouseMove;
            modeToggleButton.MouseUp += ModeButton_MouseUp;
            this.Controls.Add(modeToggleButton);
        }

        private void ModeButton_Click(object? sender, EventArgs e)
        {
            // Only toggle mode if the button was not dragged (i.e., it was a genuine click).
            if (!buttonDragged)
                ToggleMode();
            buttonDragged = false; // Reset flag for next interaction.
        }

        private void ModeButton_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDraggingButton = true;
                buttonDragged = false;
                dragButtonStart = new Point(e.X, e.Y);
                modeToggleButton.Capture = true;  // Capture mouse events even when outside the button.
            }
        }

        private void ModeButton_MouseMove(object? sender, MouseEventArgs e)
        {
            if (isDraggingButton)
            {
                // Compute how much the mouse has moved since the drag started.
                int deltaX = e.X - dragButtonStart.X;
                int deltaY = e.Y - dragButtonStart.Y;
                // If movement exceeds a small threshold, consider it a drag (prevents accidental click).
                if (Math.Abs(deltaX) > 3 || Math.Abs(deltaY) > 3)
                    buttonDragged = true;

                int newX = modeToggleButton.Left + deltaX;
                int newY = modeToggleButton.Top + deltaY;
                // Keep the button within the screen bounds.
                newX = Math.Max(0, Math.Min(newX, this.Width - modeToggleButton.Width));
                newY = Math.Max(0, Math.Min(newY, this.Height - modeToggleButton.Height));
                modeToggleButton.Location = new Point(newX, newY);
            }
        }

        private void ModeButton_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDraggingButton = false;
                modeToggleButton.Capture = false;
            }
        }

        // ====================================================================
        // Timer tick: update cursor position and portal state
        // ====================================================================
        private void PositionTimer_Tick(object? sender, EventArgs e)
        {
            if (editMode) return; // Only run in Play Mode.
            actualCursorPos = Cursor.Position;
            UpdatePortalState();
        }

        // ====================================================================
        // Helper methods for collision detection
        // ====================================================================
        /// <summary>
        /// Returns the rectangle of the custom cursor sprite in screen coordinates.
        /// The rectangle is computed from the cursor's screen position, the hotspot,
        /// and the optional offset.
        /// </summary>
        private Rectangle GetCursorSpriteRect(Point cursorPos)
        {
            int x = cursorPos.X - hotspotX + cursorOffset.X;
            int y = cursorPos.Y - hotspotY + cursorOffset.Y;
            return new Rectangle(x, y, spriteWidth, spriteHeight);
        }

        /// <summary>
        /// Determines whether the cursor sprite overlaps the given portal's bounding box.
        /// This is the primary collision check used for both entry and exit detection.
        /// </summary>
        private bool SpriteOverlapsPortal(Portal portal, Point cursorPos)
        {
            if (portal == null) return false;
            Rectangle spriteRect = GetCursorSpriteRect(cursorPos);
            return spriteRect.IntersectsWith(portal.Bounds);
        }

        // ====================================================================
        // Main portal logic: entry, split, teleport, exit
        // ====================================================================
        /// <summary>
        /// Updates the portal state based on the current cursor position.
        /// This method is called every timer tick in Play Mode.
        /// It handles:
        ///   - Switching between the system cursor and custom sprite when entering/leaving portals.
        ///   - Starting a split when the cursor enters a portal.
        ///   - Checking for complete crossing of the midline (full teleport).
        ///   - Teleporting the system cursor when the sprite fully leaves the portal on the far side.
        ///   - Preventing immediate re‑entry after teleport with a single‑frame flag.
        /// </summary>
        private void UpdatePortalState()
        {
            if (bluePortal == null || orangePortal == null) return;

            // Determine which portal the sprite currently overlaps.
            Portal? currentOverlap = null;
            Portal? currentCounterpart = null;
            if (SpriteOverlapsPortal(bluePortal, actualCursorPos))
            {
                currentOverlap = bluePortal;
                currentCounterpart = orangePortal;
            }
            else if (SpriteOverlapsPortal(orangePortal, actualCursorPos))
            {
                currentOverlap = orangePortal;
                currentCounterpart = bluePortal;
            }

            // --- Cursor visibility management ---
            // Hide the system cursor and show the custom sprite when inside a portal,
            // otherwise show the system cursor and hide the custom sprite.
            bool nowInside = currentOverlap != null;
            if (nowInside != insidePortal)
            {
                insidePortal = nowInside;
                if (insidePortal)
                {
                    this.Cursor = transparentCursor ?? Cursors.Default;
                    Cursor.Hide();
                }
                else
                {
                    this.Cursor = Cursors.Default;
                    Cursor.Show();
                }
            }

            // --- Single‑frame delay after teleport to prevent an immediate new split ---
            if (justTeleported)
            {
                justTeleported = false;
                // If we were in a split state, clear it (should not happen, but safe).
                if (isSplitting)
                {
                    isSplitting = false;
                    entrancePortal = null;
                    exitPortal = null;
                }
                Invalidate();
                return;
            }

            // --- Split logic ---
            if (isSplitting)
            {
                // If the sprite has left the entrance portal entirely...
                if (!SpriteOverlapsPortal(entrancePortal!, actualCursorPos))
                {
                    // Determine whether it left through the far side (the side opposite the entry).
                    bool leftThroughFarSide = false;
                    if (!enteringFromRight) // entered from left, far side is right
                        leftThroughFarSide = actualCursorPos.X > entrancePortal!.Bounds.Right;
                    else // entered from right, far side is left
                        leftThroughFarSide = actualCursorPos.X < entrancePortal!.Bounds.Left;

                    if (leftThroughFarSide)
                    {
                        // Teleport the system cursor to the exit portal.
                        Point target = MapCursorToExit(actualCursorPos, entrancePortal!, exitPortal!);
                        Cursor.Position = target;
                        actualCursorPos = target;
                        justTeleported = true; // prevent starting a new split in the next frame.
                    }
                    // Cancel the split regardless of whether teleport occurred.
                    isSplitting = false;
                    entrancePortal = null;
                    exitPortal = null;
                }
                else
                {
                    // Still overlapping the entrance portal. Check if the entire sprite has crossed the midline.
                    int midline = entrancePortal!.MidlineX;
                    int half = spriteWidth / 2;
                    bool fullyCrossed = false;

                    if (!enteringFromRight) // moving left to right
                    {
                        // The left edge of the sprite is the leading edge.
                        int leftEdge = actualCursorPos.X - hotspotX + cursorOffset.X;
                        fullyCrossed = leftEdge >= midline;
                    }
                    else // moving right to left
                    {
                        // The right edge of the sprite is the leading edge.
                        int rightEdge = actualCursorPos.X - hotspotX + cursorOffset.X + spriteWidth;
                        fullyCrossed = rightEdge <= midline;
                    }

                    if (fullyCrossed)
                    {
                        // Teleport immediately (the entire sprite is now on the exit side).
                        Point target = MapCursorToExit(actualCursorPos, entrancePortal, exitPortal!);
                        Cursor.Position = target;
                        actualCursorPos = target;
                        justTeleported = true;
                        isSplitting = false;
                        entrancePortal = null;
                        exitPortal = null;
                    }
                }
            }
            else
            {
                // Not currently splitting. Start a split if the cursor is inside a portal.
                if (currentOverlap != null)
                {
                    // Determine which side of the portal's midline the cursor centre is on.
                    int midline = currentOverlap.MidlineX;
                    bool fromRight = actualCursorPos.X > midline;
                    // Avoid starting a split exactly on the midline (to prevent flicker).
                    if (Math.Abs(actualCursorPos.X - midline) > 1e-3)
                    {
                        entrancePortal = currentOverlap;
                        exitPortal = currentCounterpart;
                        enteringFromRight = fromRight;
                        isSplitting = true;
                    }
                }
            }

            // Request a repaint to update the custom sprite drawing.
            Invalidate();
        }

        // ====================================================================
        // Helper: map a point from the source portal to the target portal
        // ====================================================================
        /// <summary>
        /// Given a point inside the source portal, returns the corresponding point inside the target portal
        /// by preserving the relative horizontal and vertical position (0..1).
        /// The resulting point is clamped slightly inside the portal to avoid edge‑of‑portal issues.
        /// </summary>
        private Point MapCursorToExit(Point sourcePoint, Portal sourcePortal, Portal targetPortal)
        {
            Rectangle src = sourcePortal.Bounds;
            Rectangle tgt = targetPortal.Bounds;
            double fx = Math.Clamp((double)(sourcePoint.X - src.Left) / src.Width, 0.02, 0.98);
            double fy = Math.Clamp((double)(sourcePoint.Y - src.Top) / src.Height, 0.02, 0.98);
            return new Point(tgt.Left + (int)(fx * tgt.Width),
                             tgt.Top + (int)(fy * tgt.Height));
        }

        // ====================================================================
        // Drawing methods
        // ====================================================================
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            bluePortal?.Draw(e.Graphics);
            orangePortal?.Draw(e.Graphics);

            // Only draw the custom cursor sprite in Play Mode and when the cursor is inside a portal.
            if (!editMode && insidePortal && cursorSprite != null)
            {
                if (isSplitting && entrancePortal != null && exitPortal != null)
                    DrawSplitCursor(e.Graphics, actualCursorPos, entrancePortal, exitPortal, enteringFromRight);
                else
                    DrawNormalCursor(e.Graphics, actualCursorPos);
            }
        }

        /// <summary>
        /// Draws the custom cursor sprite normally (no split).
        /// </summary>
        private void DrawNormalCursor(Graphics g, Point cursorPos)
        {
            if (cursorSprite == null) return;
            int x = cursorPos.X - hotspotX + cursorOffset.X;
            int y = cursorPos.Y - hotspotY + cursorOffset.Y;
            g.DrawImage(cursorSprite, new Rectangle(x, y, spriteWidth, spriteHeight), 0, 0, cursorSprite.Width, cursorSprite.Height, GraphicsUnit.Pixel);
        }

        /// <summary>
        /// Draws the custom cursor sprite split across two portals.
        /// The sprite is cut at the entrance portal's midline; the part on the exit side is drawn at the exit portal,
        /// while the part on the entry side remains at the entrance portal.
        /// </summary>
        private void DrawSplitCursor(Graphics g, Point cursorPos, Portal entrance, Portal exit, bool fromRight)
        {
            if (cursorSprite == null) return;
            int midline = entrance.MidlineX;
            int left = cursorPos.X - hotspotX + cursorOffset.X;
            int right = left + spriteWidth;
            int top = cursorPos.Y - hotspotY + cursorOffset.Y;
            int splitX = Math.Clamp(midline, left, right);
            int leftWidth = splitX - left;
            int rightWidth = right - splitX;

            if (!fromRight) // Entering from left: right side passes first.
            {
                // Draw the left part (still at entrance) at the entrance portal.
                if (leftWidth > 0)
                {
                    Point entrancePos = MapCursorToExit(cursorPos, entrance, entrance);
                    int dstX = entrancePos.X - hotspotX + cursorOffset.X;
                    int dstY = entrancePos.Y - hotspotY + cursorOffset.Y;
                    Rectangle dst = new Rectangle(dstX, dstY, leftWidth, spriteHeight);
                    Rectangle src = new Rectangle(0, 0, (int)(leftWidth / cursorScale), cursorSprite.Height);
                    g.DrawImage(cursorSprite, dst, src, GraphicsUnit.Pixel);
                }
                // Draw the right part (passed) at the exit portal, placed to the right of the left part.
                if (rightWidth > 0)
                {
                    Point exitPos = MapCursorToExit(cursorPos, entrance, exit);
                    int dstX = exitPos.X - hotspotX + cursorOffset.X + leftWidth;
                    int dstY = exitPos.Y - hotspotY + cursorOffset.Y;
                    Rectangle dst = new Rectangle(dstX, dstY, rightWidth, spriteHeight);
                    Rectangle src = new Rectangle((int)(leftWidth / cursorScale), 0,
                        (int)(rightWidth / cursorScale), cursorSprite.Height);
                    g.DrawImage(cursorSprite, dst, src, GraphicsUnit.Pixel);
                }
            }
            else // Entering from right: left side passes first.
            {
                // Draw the right part (still at entrance) at the entrance portal, offset to the right.
                if (rightWidth > 0)
                {
                    Point entrancePos = MapCursorToExit(cursorPos, entrance, entrance);
                    int dstX = entrancePos.X - hotspotX + cursorOffset.X + leftWidth;
                    int dstY = entrancePos.Y - hotspotY + cursorOffset.Y;
                    Rectangle dst = new Rectangle(dstX, dstY, rightWidth, spriteHeight);
                    Rectangle src = new Rectangle((int)(leftWidth / cursorScale), 0,
                        (int)(rightWidth / cursorScale), cursorSprite.Height);
                    g.DrawImage(cursorSprite, dst, src, GraphicsUnit.Pixel);
                }
                // Draw the left part (passed) at the exit portal.
                if (leftWidth > 0)
                {
                    Point exitPos = MapCursorToExit(cursorPos, entrance, exit);
                    int dstX = exitPos.X - hotspotX + cursorOffset.X;
                    int dstY = exitPos.Y - hotspotY + cursorOffset.Y;
                    Rectangle dst = new Rectangle(dstX, dstY, leftWidth, spriteHeight);
                    Rectangle src = new Rectangle(0, 0, (int)(leftWidth / cursorScale), cursorSprite.Height);
                    g.DrawImage(cursorSprite, dst, src, GraphicsUnit.Pixel);
                }
            }
        }

        // ====================================================================
        // Edit Mode dragging (portals)
        // ====================================================================
        /// <summary>
        /// In Edit Mode, when the mouse is pressed inside a portal, start dragging it.
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!editMode) return;
            if (bluePortal == null || orangePortal == null) return;
            Point cursorScreen = Cursor.Position;
            // Check if the cursor centre is inside the portal (the entire sprite is used for drawing,
            // but for dragging we use the centre for simplicity).
            if (bluePortal.Contains(cursorScreen))
            {
                draggingPortal = bluePortal;
                dragStartCursor = cursorScreen;
                dragStartPortalCenter = bluePortal.Center;
            }
            else if (orangePortal.Contains(cursorScreen))
            {
                draggingPortal = orangePortal;
                dragStartCursor = cursorScreen;
                dragStartPortalCenter = orangePortal.Center;
            }
            base.OnMouseDown(e);
        }

        /// <summary>
        /// While dragging a portal, update its centre position based on mouse movement.
        /// </summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!editMode || draggingPortal == null) return;
            Point cursorNow = Cursor.Position;
            int deltaX = cursorNow.X - dragStartCursor.X;
            int deltaY = cursorNow.Y - dragStartCursor.Y;
            Point newCenter = new Point(dragStartPortalCenter.X + deltaX, dragStartPortalCenter.Y + deltaY);
            draggingPortal.Center = newCenter;
            Invalidate();
            base.OnMouseMove(e);
        }

        /// <summary>
        /// End portal dragging.
        /// </summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            draggingPortal = null;
            base.OnMouseUp(e);
        }

        // ====================================================================
        // Mode toggling and message forwarding
        // ====================================================================
        /// <summary>
        /// Switches between Edit Mode (dragging portals) and Play Mode (cursor teleport).
        /// Resets all portal state and updates the button text.
        /// </summary>
        private void ToggleMode()
        {
            editMode = !editMode;
            if (editMode)
            {
                Cursor.Show();
                this.Cursor = Cursors.Default;
                modeToggleButton.Text = "EDIT MODE";
                SetClickThrough(false);
                // Reset all portal state.
                isSplitting = false;
                entrancePortal = null;
                exitPortal = null;
                justTeleported = false;
                insidePortal = false;
            }
            else
            {
                this.Cursor = Cursors.Default;
                Cursor.Show(); // start with system cursor visible
                modeToggleButton.Text = "PLAY MODE";
                SetClickThrough(false);
                // Reset all portal state.
                isSplitting = false;
                entrancePortal = null;
                exitPortal = null;
                justTeleported = false;
                insidePortal = false;
            }
            actualCursorPos = Cursor.Position;
            UpdatePortalState();
            Invalidate();
        }

        /// <summary>
        /// Toggles the WS_EX_TRANSPARENT style on the form.
        /// When enabled, the form passes mouse clicks through to the underlying windows.
        /// When disabled, it captures them.
        /// </summary>
        private void SetClickThrough(bool clickThrough)
        {
            int exStyle = NativeMethods.GetWindowLong(this.Handle, NativeMethods.GWL_EXSTYLE);
            if (clickThrough)
                exStyle |= NativeMethods.WS_EX_TRANSPARENT;
            else
                exStyle &= ~NativeMethods.WS_EX_TRANSPARENT;
            NativeMethods.SetWindowLong(this.Handle, NativeMethods.GWL_EXSTYLE, exStyle);
            NativeMethods.SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
        }

        /// <summary>
        /// Overrides the window message handler to:
        ///   - Handle hotkey (Escape to exit).
        ///   - Handle system command to minimize the window when the taskbar icon is clicked.
        ///   - Forward mouse messages to the underlying window when in Play Mode and not splitting
        ///     (this provides click‑through behaviour without using WS_EX_TRANSPARENT).
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            // Hotkey handling (Escape).
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_ID_EXIT)
                {
                    Application.Exit();
                    return;
                }
            }

            // Taskbar icon minimise/restore.
            if (m.Msg == WM_SYSCOMMAND && (int)m.WParam == SC_MINIMIZE)
            {
                this.WindowState = FormWindowState.Minimized;
                return;
            }

            // In Play Mode, when not splitting, forward mouse messages to the window under the cursor.
            if (!editMode && !isSplitting)
            {
                switch (m.Msg)
                {
                    case WM_LBUTTONDOWN:
                    case WM_LBUTTONUP:
                    case WM_RBUTTONDOWN:
                    case WM_RBUTTONUP:
                    case WM_MOUSEMOVE:
                    case WM_LBUTTONDBLCLK:
                        NativeMethods.GetCursorPos(out NativeMethods.POINT pt);
                        IntPtr hWnd = NativeMethods.WindowFromPoint(pt);
                        if (hWnd != this.Handle && hWnd != IntPtr.Zero)
                        {
                            NativeMethods.PostMessage(hWnd, (uint)m.Msg, m.WParam, m.LParam);
                            return;
                        }
                        break;
                }
            }

            base.WndProc(ref m);
        }

        // ====================================================================
        // Global hotkey registration
        // ====================================================================
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private void RegisterHotkeys()
        {
            RegisterHotKey(this.Handle, HOTKEY_ID_EXIT, 0, (uint)Keys.Escape);
        }

        // ====================================================================
        // Clean up resources when the form closes
        // ====================================================================
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            positionTimer?.Stop();
            positionTimer?.Dispose();
            gifTimer?.Stop();
            gifTimer?.Dispose();
            UnregisterHotKey(this.Handle, HOTKEY_ID_EXIT);

            // Stop any running GIF animations.
            if (bluePortal?.Image != null)
                ImageAnimator.StopAnimate(bluePortal.Image, (sender, args) => { });
            if (orangePortal?.Image != null)
                ImageAnimator.StopAnimate(orangePortal.Image, (sender, args) => { });

            Cursor.Show();
            this.Cursor = Cursors.Default;
            base.OnFormClosing(e);
        }
    }
}