﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grandma.Core
{
    [RequireComponent(typeof(GrandmaObject))]
    public abstract class GrandmaComponent : MonoBehaviour
    {
        public GrandmaObject Base { get; private set; }

        [Tooltip("Should this component use the canonical Scriptable Object or create an instance for its own use?")]
        public bool duplicateData = true;
        public GrandmaComponentData Data;

        /// <summary>
        /// Called when some data field has been updated
        /// </summary>
        public Action<GrandmaComponent> OnUpdated;

        public string ObjectID
        {
            get
            {
                return Base.Data.id;
            }
        }

        protected virtual void Awake()
        {
            Base = GetComponent<GrandmaObject>();

            if (Base == null)
            {
                Base = gameObject.AddComponent<GrandmaObject>();
            }

            if(Base.Data == null)
            {
                Base.RegisterWithManager();
            }

            if (Data != null)
            {
                /*
                 * Issue: Does not deep copy
                 * 
                 * TODO: find solution to deep copying scriptableobjects
                if (duplicateData)
                {
                    Data = Instantiate(Data);
                }
                */

                OnRead(Data);

                if (OnUpdated != null)
                {
                    OnUpdated(this);
                }
            }
        }

        protected virtual void Start() { }

        /// <summary>
        /// Set component state from some provided data
        /// </summary>
        /// <param name="data"></param>
        public void Read(GrandmaComponentData data)
        {
            this.Data = data;

            OnRead(data);

            if(OnUpdated != null)
            {
                OnUpdated(this);
            }
        }

        protected virtual void OnRead(GrandmaComponentData data) { }

        #region Write
        /// <summary>
        /// Produce a JSON representation of this component
        /// </summary>
        /// <returns></returns>
        public string WriteToJSON()
        {
            if (ValidateState() == false)
            {
                Debug.LogWarning("GrandmaComponent: Cannot Write as Data is invalid");
                return null;
            }

            //Give the component an opportunity to reach out and update any fields it needs to before write
            OnWrite();

            return JsonUtility.ToJson(this.Data);
        }

        //Helper - alert that data has changed
        protected virtual void Write()
        {
            if (ValidateState() == false)
            {
                Debug.LogWarning("GrandmaComponent: Cannot Write as Data is invalid");
                return;
            }

            Data.associatedObjID = Base.Data.id;
            //Send to network 

            //Update based on new Data
            Read(Data);
        }

        /// <summary>
        /// Gives the component a chance to repopulate variables before a Write
        /// </summary>
        protected virtual void OnWrite() { }
        #endregion

        /// <summary>
        /// Is this GrandmaComponent valid?
        /// </summary>
        /// <returns></returns>
        protected virtual bool ValidateState()
        {
            return Data != null && Data.IsValid;
        }
    }
}
