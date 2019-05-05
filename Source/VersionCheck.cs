//  ================================================================================
//  Copyright © 2014 - 2019, Majiir, ferram4.
//
//  All rights reserved.
//
//  Redistribution and use in source and binary forms, with or without modification,
//  are permitted provided that the following conditions are met:
//
//  1. Redistributions of source code must retain the above copyright notice, this
//     list of conditions and the following disclaimer.
//
//  2. Redistributions in binary form must reproduce the above copyright notice, this
//     list of conditions and the following disclaimer in the documentation and/or
//     other materials provided with the distribution.
//
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
//  ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
//  WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
//  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
//  INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
//  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
//  OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
//  WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
//  ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
//  POSSIBILITY OF SUCH DAMAGE.
//  ================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RSSVE
{
    //  This utility displays a warning with a list of mods that determine themselves
    //  to be incompatible with the current running version of Kerbal Space Program.
    //
    //  See the following forum thread for details:
    //  https://forum.kerbalspaceprogram.com/index.php?showtopic=59388

    [KSPAddon (KSPAddon.Startup.Instantly, true)]

    class CompatibilityChecker : MonoBehaviour
    {
        public static bool IsCompatible ()
        {
            //  If you want to disable some behavior when incompatible, other
            //  parts of the plugin should query this method:
            //
            //      if (!CompatibilityChecker.IsCompatible ())
            //      {
            //          ...disable some features...
            //      }
            //
            //  Even if you don't lock down functionality, you should return true
            //  if your users can expect a future update to be available.

            return Utilities.InRange (Versioning.version_minor, Constants.VersionCompatible.MinorMin, Constants.VersionCompatible.MinorMax) &&
                   Utilities.InRange (Versioning.version_major, Constants.VersionCompatible.MajorMin, Constants.VersionCompatible.MajorMax) &&
                   Utilities.InRange (Versioning.Revision, Constants.VersionCompatible.RevisionMin, Constants.VersionCompatible.RevisionMax);
        }

        public static bool IsUnityCompatible ()
        {
            //  Check if the version of Unity used by KSP is compatible
            //  with the version of Unity that the mod expects.

            return (Application.unityVersion.Equals (Constants.UnityVersion));
        }

        //  Version of the compatibility checker itself.

        static int _version = 6;

        void Start ()
        {
            // Checkers are identified by the type name and version field name.

            var fields = GetAllTypes ()
                .Where (t => t.Name.Equals ("CompatibilityChecker"))
                .Select (t => t.GetField ("_version", BindingFlags.Static | BindingFlags.NonPublic))
                .Where (f => f != null)
                .Where (f => f.FieldType.Equals (typeof (int)))
                .ToArray ();

            //  Let the latest version of the checker execute.

            if (_version != fields.Max (f => (int) f.GetValue (null)))
            {
                return;
            }

            Debug.Log (string.Format ("[CompatibilityChecker]: Running checker version {0} from '{1}'", _version, Assembly.GetExecutingAssembly ().GetName ().Name));

            //  Other checkers will see this version and not run.
            //  This accomplishes the same as an explicit "ran" flag with fewer moving parts.

            _version = int.MaxValue;

            //  A mod is incompatible if its compatibility checker has an IsCompatible method which returns false.

            var incompatible = fields
                .Select (f => f.DeclaringType.GetMethod ("IsCompatible", Type.EmptyTypes))
                .Where (m => m.IsStatic)
                .Where (m => m.ReturnType.Equals (typeof (bool)))
                .Where (m =>
                {
                    try
                    {
                        return !(bool) m.Invoke (null, new object [0]);
                    }
                    catch (Exception e)
                    {
                        //  If a mod throws an exception from IsCompatible, it's not compatible.

                        Debug.LogWarning (string.Format ("[CompatibilityChecker]: Exception while invoking IsCompatible() from '{0}':\n\n{1}", m.DeclaringType.Assembly.GetName ().Name, e));

                        return true;
                    }
                })
                .Select (m => m.DeclaringType.Assembly.GetName ().Name)
                .ToArray ();

            //  A mod is incompatible with Unity if its compatibility checker has an IsUnityCompatible method which returns false.

            var incompatibleUnity = fields
                .Select (f => f.DeclaringType.GetMethod ("IsUnityCompatible", Type.EmptyTypes))
                .Where (m => m != null)  //  Mods without IsUnityCompatible () are assumed to be compatible.
                .Where (m => m.IsStatic)
                .Where (m => m.ReturnType.Equals (typeof (bool)))
                .Where (m =>
                {
                    try
                    {
                        return !(bool) m.Invoke (null, new object [0]);
                    }
                    catch (Exception e)
                    {
                        //  If a mod throws an exception from IsUnityCompatible, it's not compatible.

                        Debug.LogWarning (string.Format ("[CompatibilityChecker]: Exception while invoking IsUnityCompatible() from '{0}':\n\n{1}", m.DeclaringType.Assembly.GetName ().Name, e));

                        return true;
                    }
                })
                .Select (m => m.DeclaringType.Assembly.GetName ().Name)
                .ToArray ();

            Array.Sort (incompatible);
            Array.Sort (incompatibleUnity);

            string message = string.Empty;

            if ((incompatible.Length > 0) || (incompatibleUnity.Length > 0))
            {
                message += ((message.Equals (string.Empty)) ? "Some" : "\n\nAdditionally, some") + " installed mods may be incompatible with this version of Kerbal Space Program. Features may be broken or disabled. Please check for updates to the listed mods.";

                if (incompatible.Length > 0)
                {
                    Debug.LogWarning ("[CompatibilityChecker]: Incompatible mods detected: " + string.Join (", ", incompatible));

                    message += string.Format ("\n\nThese mods are incompatible with KSP {0}.{1}.{2}:\n\n", Versioning.version_major, Versioning.version_minor, Versioning.Revision);
                    message += string.Join ("\n", incompatible);
                }

                if (incompatibleUnity.Length > 0)
                {
                    Debug.LogWarning ("[CompatibilityChecker]: Incompatible mods (Unity) detected: " + string.Join (", ", incompatibleUnity));

                    message += string.Format ("\n\nThese mods are incompatible with Unity {0}:\n\n", Application.unityVersion);
                    message += string.Join ("\n", incompatibleUnity);
                }
            }

            if ((incompatible.Length > 0) || (incompatibleUnity.Length > 0))
            {
                PopupDialog.SpawnPopupDialog (new Vector2 (0.0f, 0.0f), new Vector2 (0.0f, 0.0f), "CompatibilityChecker", "Incompatible Mods Detected", message, "OK", true, HighLogic.UISkin, true, string.Empty);
            }
        }

        static IEnumerable<Type> GetAllTypes ()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies ())
            {
                Type [] types;

                try
                {
                    types = assembly.GetTypes ();
                }
                catch (Exception)
                {
                    types = Type.EmptyTypes;
                }

                foreach (var type in types)
                {
                    yield return type;
                }
            }
        }
    }
}
