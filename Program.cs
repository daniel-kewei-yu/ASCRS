// ============================================================================
// File:        Program.cs
// Project:     Aperture Science Cursor Relocation Suite (ASCRS)
// Author:      Daniel Yu
// Date:        2026-03-27
// Description:
//   The entry point of the application. It enables visual styles, starts the
//   main message loop with MainForm, and ensures that the system cursor is
//   shown when the application exits (as a safety measure).
// ============================================================================

using System;
using System.Windows.Forms;
using ASCRS.Forms;

namespace ASCRS
{
    /// <summary>
    /// Contains the main entry point for the application.
    /// </summary>
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]  // Required for Windows Forms to work correctly
        static void Main()
        {
            try
            {
                // Enable visual styles (e.g., themed controls) and set the
                // default rendering mode for text.
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Start the application's main message loop with the main form.
                Application.Run(new MainForm());
            }
            finally
            {
                // Ensure the system cursor is visible when the application exits.
                // This is a safety net in case the cursor was hidden during
                // normal operation and the program terminates unexpectedly.
                Cursor.Show();
            }
        }
    }
}