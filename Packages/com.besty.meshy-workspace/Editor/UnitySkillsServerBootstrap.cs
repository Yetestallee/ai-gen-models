using System;
using UnityEditor;
using UnitySkills;
using UnityEngine;

namespace MeshyWorkspace.Editor
{
    /// <summary>
    /// Ensures the UnitySkills REST server starts on port 8090 after every
    /// domain reload so the workspace remains controllable from Codex.
    /// </summary>
    [InitializeOnLoad]
    public static class UnitySkillsServerBootstrap
    {
        static UnitySkillsServerBootstrap()
        {
            Debug.Log("[Meshy] UnitySkillsServerBootstrap ctor");
            EditorApplication.delayCall += () =>
            {
                Debug.Log("[Meshy] UnitySkillsServerBootstrap delayCall");
                try
                {
                    SkillsHttpServer.AutoStart = true;
                    SkillsHttpServer.Start(8090, fallbackToAuto: true);
                    Debug.Log("[Meshy] SkillsHttpServer.Start called, port=" + SkillsHttpServer.Port);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Meshy] SkillsHttpServer.Start failed: " + e);
                }
            };
        }
    }
}
