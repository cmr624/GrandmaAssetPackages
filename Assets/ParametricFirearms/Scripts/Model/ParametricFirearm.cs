﻿using System;
using System.Collections;
using UnityEngine;

using Grandma.Core;

namespace Grandma.ParametricFirearms
{
    public class ParametricFirearm : GrandmaComponent
    {
        public PFProjectile projectilePrefab;
        [Tooltip("Where the projectile will spawn from and its initial direction (z-axis)")]
        public Transform barrelTip;

        //Data Properties
        [NonSerialized]
        private PFData pfData;

        //Ammo remaining in the clip
        public int CurrentAmmo { get; private set; }

        #region State Management
        public enum PFState
        {
            Ready,
            Charging,
            CoolDown,
            //CoolDownInterupt,
            ManualReload
        }

        private PFState state;

        public PFState State
        {
            get
            {
                return state;
            }
            private set
            {
                state = value;

                if (onStateChanged != null)
                {
                    onStateChanged(value);
                }
            }
        }

        public Action<PFState> onStateChanged;

        private Coroutine chargeCoroutine;
        private Coroutine manaualReloadCoroutine;
        private Coroutine coolDownCoroutine;
        #endregion

        #region Events
        [Header("Events")]
        public PFEvent OnTriggerPressed;
        public PFPercentageEvent OnCharge;
        public PFEvent OnTriggerReleased;
        public PFEvent OnChargeCancelled;
        public PFEvent OnFire;
        public PFPercentageEvent OnCoolDown;
        public PFEvent OnManualReload;
        public PFEvent OnCancelManualReload;
        public PFEvent OnCoolDownComplete;
        #endregion

        public override void Read(GrandmaComponentData data)
        {
            base.Read(data);

            pfData = data as PFData;

            //Change me
            CurrentAmmo = pfData.RateOfFire.AmmoCapacity;
        }

        #region Public Weapon Methods
        /// <summary>
        /// When in Ready state, will begin charging the weapon. NB if chargeTime is 0, will immediately call fire
        /// </summary>
        public void TriggerPress()
        {
            if (State == PFState.Ready)
            {
                State = PFState.Charging;
                chargeCoroutine = StartCoroutine(Charge());

                if(OnTriggerPressed != null)
                {
                    OnTriggerPressed.Invoke(this);
                }
            }
        }

        /// <summary>
        /// If in Charging state, will either stop charging or fire depending on Data
        /// </summary>
        public void TriggerRelease()
        {
            if (State == PFState.Charging)
            {
                //Interupt charging
                StopCoroutine(chargeCoroutine);

                if (OnTriggerReleased != null)
                {
                    OnTriggerReleased.Invoke(this);
                }

                if (pfData.ChargeTime.requireFullyCharged == false)
                {
                    //Fire
                    Fire();
                }
                else
                {
                    //Cancel
                    State = PFState.Ready;

                    if (OnChargeCancelled != null)
                    {
                        OnChargeCancelled.Invoke(this);
                    }
                }
            }
        }

        public void ToggleManualReload()
        {
            if(State == PFState.ManualReload)
            {
                CancelManualReload();
            }
            else
            {
                ManualReload();
            }
        }

        /// <summary>
        /// If in Ready or Charging, will begin a manual reload
        /// </summary>
        public void ManualReload()
        {
            if (State == PFState.Ready || State == PFState.Charging)
            {
                if (chargeCoroutine != null)
                {
                    StopCoroutine(chargeCoroutine);
                }

                State = PFState.ManualReload;
                manaualReloadCoroutine = StartCoroutine(ManualReload_CO());

                if (OnManualReload != null)
                {
                    OnManualReload.Invoke(this);
                }
            }
        }

        /// <summary>
        /// If ManualReload, will switch back to ready
        /// </summary>
        public void CancelManualReload()
        {
            if(State == PFState.ManualReload)
            {
                StopCoroutine(manaualReloadCoroutine);
                State = PFState.Ready;

                if (OnManualReload != null)
                {
                    OnManualReload.Invoke(this);
                }
            }
        }

        /*
        public void ResumeCoolDown()
        {
            if (State == PFState.CoolDownInterupt)
            {
                State = PFState.CoolDown;
                coolDownCoroutine = StartCoroutine(CoolDown());
            }
        }

        public void InteruptCoolDown()
        {
            if (State == PFState.CoolDown)
            {
                State = PFState.CoolDownInterupt;
                StopCoroutine(coolDownCoroutine);
            }
        }
        */
        #endregion

        #region Private Weapon Methods
        /// <summary>
        /// Launches projectile(s) and transistions into cool down
        /// </summary>    
        private void Fire()
        {
            if(projectilePrefab == null)
            {
                Debug.LogWarning("ParametricFirearm: Unable to fire as projectile prefab is null");
                return;
            }

            for (int i = 0; i < pfData.Multishot.numberOfShots; i++)
            {
                //Spawn the projectile
                var projectile = Instantiate(projectilePrefab);

                if(barrelTip != null)
                {
                    projectile.transform.position = barrelTip.position;
                    projectile.transform.forward = barrelTip.forward;
                }

                //Clone projectile data
                var projData = JsonUtility.FromJson<PFProjectileData>(JsonUtility.ToJson(pfData.Projectile));
                projectile.Launch(projData);

                //Controlling ROF
                //CUrrent ammo is decremented before being sent to GetWaitTime to avoid the off by one error
                CurrentAmmo--;

                //Run out of ammo - will force reload
                if (CurrentAmmo <= 0)
                {
                    break;
                }
            }

            if (OnFire != null)
            {
                OnFire.Invoke(this);
            }

            State = PFState.CoolDown;
            coolDownCoroutine = StartCoroutine(CoolDown());
        }

        private IEnumerator Charge()
        {
            float timer = 0f;

            while(timer < pfData.ChargeTime.chargeTime)
            {
                if(OnCharge != null)
                {
                    OnCharge.Invoke(this, timer);
                }

                timer += Time.deltaTime;

                yield return null;
            }

            //state is charge
            Fire();
            //state is cool down
        }

        /// <summary>
        /// Prevents the PF for firing. Used to control rate of fire and forced reloading
        /// </summary>
        /// <param name="waitTime"></param>
        /// <returns></returns>
        private IEnumerator CoolDown()
        {
            float timer = 0f;

            while(timer < pfData.RateOfFire.GetWaitTime(CurrentAmmo))
            {
                if (OnCoolDown != null)
                {
                    OnCoolDown.Invoke(this, timer);
                }

                timer += Time.deltaTime;

                yield return null;
            }

            //If was a forced reload
            if (CurrentAmmo <= 0)
            {
                CurrentAmmo = pfData.RateOfFire.AmmoCapacity;
            }

            if(OnCoolDownComplete != null)
            {
                OnCoolDownComplete.Invoke(this);
            }

            State = PFState.Ready;
        }

        private IEnumerator ManualReload_CO()
        {
            yield return new WaitForSeconds(pfData.RateOfFire.ReloadTime);

            //for now, we are assuming the Overwatch model of ammo - infinte with reloads
            CurrentAmmo = pfData.RateOfFire.AmmoCapacity;
            State = PFState.Ready;
        }
        #endregion

        public override string ToString()
        {
            return string.Format("PF named {0} is in state: {1}, has current ammo {2}", pfData.Meta.name, State.ToString(), CurrentAmmo);
        }
    }
}