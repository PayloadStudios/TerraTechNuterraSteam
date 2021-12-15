using System;
using UnityEngine;
using System.Reflection;
using HarmonyLib;


namespace CustomModules.LegacyModule
{
    public class ModuleCustomBlock : Module
    {
        public string FilePath;
        public bool HasInjectedCenterOfMass = false;
        public Vector3 InjectedCenterOfMass;
        public EmissionMode BlockEmissionMode;

        internal uint reparse_version_cache;

        private bool rbodyExists = false;
        private float emissionTimeDelay = 0f;

        public void UpdateEmission()
        {
            switch (BlockEmissionMode)
            {
                case EmissionMode.Active:
                    SetEmissionOn(); return;

                case EmissionMode.ActiveAtNight:
                    if (ManTimeOfDay.inst.NightTime) SetEmissionOn();
                    else SetEmissionOff(); return;

                case EmissionMode.ActiveWhenAnchored:
                    if (block.tank != null && block.tank.IsAnchored) SetEmissionOn();
                    else SetEmissionOff(); return;
            }
        }

        public void SetEmissionColor(Color EmissionColor)
        {
            foreach (var ren in gameObject.GetComponentsInChildren<Renderer>(true))
            {
                if (ren.material.IsKeywordEnabled("StandardTankBlock"))
                    ren.material.SetColor("_EmissionColor", EmissionColor);
            }
        }

        public void SetEmissionOn()
        {
            block.SwapMaterialTime(true);
            SetEmissionColor(Color.white);
        }
        public void SetEmissionOff()
        {
            block.SwapMaterialTime(false);
            SetEmissionColor(Color.black);
        }

        void ChangeTimeEmission(bool _)
        {
            emissionTimeDelay = UnityEngine.Random.value * 2f + 1f;
        }

        void ChangeAnchorEmission(ModuleAnchor _, bool isAnchored, bool __)
        {
            if (isAnchored)
                SetEmissionOn();
            else
                SetEmissionOff();
        }
        void HookAnchorEmission()
        {
            if (block.tank.IsAnchored) SetEmissionOn();
            block.tank.AnchorEvent.Subscribe(ChangeAnchorEmission);
        }
        void UnhookAnchorEmission()
        {
            block.tank.AnchorEvent.Unsubscribe(ChangeAnchorEmission);
            SetEmissionOff();
        }

        void OnSpawn()
        {
            switch (BlockEmissionMode)
            {
                case EmissionMode.Active:
                    SetEmissionOn(); break;

                case EmissionMode.ActiveAtNight:
                    if (ManTimeOfDay.inst.NightTime)
                        SetEmissionOn();
                    else
                        SetEmissionOff();
                    break;
            }
        }

        void OnPool()
        {
            switch (BlockEmissionMode)
            {
                case EmissionMode.ActiveAtNight:
                    ManTimeOfDay.inst.DayEndEvent.Subscribe(ChangeTimeEmission);
                    return;

                case EmissionMode.ActiveWhenAnchored:
                    block.AttachEvent.Subscribe(HookAnchorEmission);
                    block.DetachEvent.Subscribe(UnhookAnchorEmission);
                    return;

                default: return;
            }
        }

        void Update()
        {
            if (emissionTimeDelay > 0f)
            {
                emissionTimeDelay -= Time.deltaTime;
                if (emissionTimeDelay <= 0f)
                {
                    emissionTimeDelay = 0f;
                    if (ManTimeOfDay.inst.NightTime)
                        SetEmissionOn();
                    else
                        SetEmissionOff();
                }
            }
            if (HasInjectedCenterOfMass)
            {
                bool re = block.rbody.IsNotNull();
                if (re != rbodyExists)
                {
                    rbodyExists = re;
                    if (re)
                    {
                        block.rbody.centerOfMass = InjectedCenterOfMass;
                    }
                }
            }
        }

        public enum EmissionMode : byte
        {
            None,
            Active,
            ActiveAtNight,
            ActiveWhenAnchored
        }
    }
}
