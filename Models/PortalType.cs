// ============================================================================
// File:        PortalType.cs
// Project:     Aperture Science Cursor Relocation Suite (ASCRS)
// Author:      Daniel Yu
// Date:        2026-03-27
// Description:
//   Simple enumeration to distinguish between the Blue and Orange portals.
//   Currently not used in the core logic, but provided for future extensions
//   (e.g., adding more portals or colour‑coded behaviour).
// ============================================================================

namespace ASCRS.Models
{
    /// <summary>
    /// Specifies which portal is which. Useful for debugging or future
    /// features that need to differentiate the two portals.
    /// </summary>
    public enum PortalType
    {
        Blue,
        Orange
    }
}