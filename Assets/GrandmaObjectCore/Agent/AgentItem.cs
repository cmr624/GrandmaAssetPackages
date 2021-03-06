﻿using System;
using UnityEngine;

namespace Grandma.Core
{
    public class AgentItemData : GrandmaComponentData
    {
        [HideInInspector]
        public string agentID;
    }

    /// <summary>
    /// An item that can be used by an agent
    /// </summary>
    public abstract class AgentItem : GrandmaComponent
    {
        private Agent agent;
        public Agent Agent
        {
            get
            {
                return agent;
            }
            protected set
            {
                if(agent != null && agent.Items.Contains(this))
                {
                    agent.Items.Remove(this);
                }

                agent = value;

                if (agent != null && agent.Items.Contains(this) == false)
                {
                    agent.Items.Add(this);
                }
            }
        }

        protected override void OnRead(GrandmaComponentData data)
        {
            base.OnRead(data);

            var agentItemData = Data as AgentItemData;

            if (agentItemData != null)
            {
                Agent = GrandmaObjectManager.Instance.GetComponentByID(agentItemData.agentID, typeof(Agent)) as Agent;
            }
        }

        public void SetAgent(string agentID)
        {
            (Data as AgentItemData).agentID = agentID;

            Write();
        }
    }
}
