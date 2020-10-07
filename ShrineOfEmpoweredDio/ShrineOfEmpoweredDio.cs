using RoR2;
using BepInEx;
using UnityEngine;
using System.Collections.Generic;
using Random = System.Random;
using BepInEx.Configuration;
using UnityEngine.Networking;
using RoR2.Hologram;
using System;
using System.Linq;
using R2API.Utils;
using RoR2.Networking;

namespace ShrineOfEmpoweredDio
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.SirHamburger.ShrineOfEmpoweredDio", "ShrineOfEmpoweredDio", "1.3.3")]
    public class ShrineOfDio : BaseUnityPlugin
    {

        public static int ResurrectionCost = 300;

        public const int UNINITIALIZED = -2;

        public const int BALANCED_MODE = -1;

        public int clientCost = UNINITIALIZED;
        public bool isBalancedMode = true;

        private int useCount = 1;


        public void Awake()
        {


            On.RoR2.SceneDirector.PopulateScene += (orig, self) =>
            {

                orig(self);
                if(RoR2.SceneInfo.instance.sceneDef.stageOrder == 5)
                SpawnShrineOfDio(self);
                

                

            };

            On.RoR2.ShrineHealingBehavior.FixedUpdate += (orig, self) =>
            {
                if(RoR2.SceneInfo.instance.sceneDef.stageOrder != 5)
                {
                    orig(self);
                    return;
                }
                orig(self);

                if (clientCost == UNINITIALIZED)
                {
                    int piCost = self.GetFieldValue<PurchaseInteraction>("purchaseInteraction").cost;
                    if (piCost != clientCost)
                    {
                        clientCost = piCost;
                        if (clientCost == BALANCED_MODE)
                        {
                            isBalancedMode = true;
                        }
                        else
                        {
                            isBalancedMode = false;
                        }
                        Type[] arr = ((IEnumerable<System.Type>)typeof(ChestRevealer).Assembly.GetTypes()).Where<System.Type>((Func<System.Type, bool>)(t => typeof(IInteractable).IsAssignableFrom(t))).ToArray<System.Type>();
                        for (int i = 0; i < arr.Length; i++)
                        {
                            foreach (UnityEngine.MonoBehaviour instances in InstanceTracker.FindInstancesEnumerable(arr[i]))
                            {
                                if (((IInteractable)instances).ShouldShowOnScanner())
                                {
                                    string item = ((IInteractable)instances).ToString().ToLower();
                                    if (item.Contains("shrinehealing"))
                                    {
                                        UpdateShrineDisplay(instances.GetComponentInParent<ShrineHealingBehavior>());
                                    }
                                };
                            }
                        }

                    }
                }

            };

            On.RoR2.ShrineHealingBehavior.Awake += (orig, self) =>
            {
                if(RoR2.SceneInfo.instance.sceneDef.stageOrder != 5)
                {
                    orig(self);
                    return;
                }
                orig(self);

                PurchaseInteraction pi = self.GetFieldValue<PurchaseInteraction>("purchaseInteraction");
                pi.contextToken = "Offer to the Shrine of empowered Dio";
                pi.displayNameToken = "Shrine of empowered Dio";
                self.costMultiplierPerPurchase = 4f;




                    pi.costType = CostTypeIndex.Money;
                    //pi.cost = ResurrectionCost.Value * useCount;
                    pi.cost=GetDifficultyScaledCost(ResurrectionCost);

            };

            On.RoR2.ShrineHealingBehavior.AddShrineStack += (orig, self, interactor) =>
            {
                if(RoR2.SceneInfo.instance.sceneDef.stageOrder != 5)
                {
                    orig(self,interactor);
                    return;
                }
                string resurrectionMessage = $"<color=#beeca1>{interactor.GetComponent<CharacterBody>().GetUserName()}</color> gained a <color=#beeca1>Dio</color>";
                Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                {
                    baseToken = resurrectionMessage
                });
                self.SetFieldValue("waitingForRefresh", true);
                self.SetFieldValue("refreshTimer", 2f);
                EffectManager.SpawnEffect(Resources.Load<GameObject>("Prefabs/Effects/ShrineUseEffect"), new EffectData()
                {
                    origin = self.transform.position,
                    rotation = Quaternion.identity,
                    scale = 1f,
                    color = (Color32)Color.red
                }, true);
                // dio
                CharacterBody cb = interactor.GetComponent<CharacterBody>();

                PurchaseInteraction pi = self.GetComponent<PurchaseInteraction>();
                PurchaseInteraction.CreateItemTakenOrb(cb.corePosition, pi.gameObject, ItemIndex.ExtraLife);
                cb.inventory.RemoveItem(ItemIndex.ExtraLifeConsumed, 1);
                cb.inventory.GiveItem(ItemIndex.ExtraLife, 1);
                useCount++;


            };

            On.RoR2.PurchaseInteraction.CanBeAffordedByInteractor += (orig, self, interactor) =>
            {
                if (self.displayNameToken.Contains("Shrine of empowered Dio"))
                {
                    if (interactor.GetComponent<CharacterBody>().inventory.GetItemCount(ItemIndex.ExtraLifeConsumed) > 0)
                    {
                        return orig(self, interactor);
                    }
                    return false;
                }
                return orig(self, interactor);
            };

        }

        private void UpdateShrineDisplay(ShrineHealingBehavior self)
        {
            if(RoR2.SceneInfo.instance.sceneDef.stageOrder != 5)
                {
                    return;
                }
            PurchaseInteraction pi = self.GetFieldValue<PurchaseInteraction>("purchaseInteraction");

                pi.costType = CostTypeIndex.Money;
                pi.cost = GetDifficultyScaledCost(ResurrectionCost);//clientCost*useCount;

        }

        private int GetDifficultyScaledCost(int baseCost)
        {
            return (int)((double)baseCost * (double)Mathf.Pow(Run.instance.difficultyCoefficient, 1.25f)); // 1.25f
        }


        public void SpawnShrineOfDio(SceneDirector self)
        {
            if(RoR2.SceneInfo.instance.sceneDef.stageOrder != 5)
                {
                    return;
                }
            Xoroshiro128Plus xoroshiro128Plus = new Xoroshiro128Plus(self.GetFieldValue<Xoroshiro128Plus>("rng").nextUlong);
            if (SceneInfo.instance.countsAsStage)
            {
                SpawnCard card = Resources.Load<SpawnCard>("SpawnCards/InteractableSpawnCard/iscShrineHealing");

                GameObject gameObject3 = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(card, new DirectorPlacementRule
                {
                    placementMode = DirectorPlacementRule.PlacementMode.Random
                }, xoroshiro128Plus));

                    gameObject3.GetComponent<PurchaseInteraction>().Networkcost = GetDifficultyScaledCost(ResurrectionCost);
                }
            
        }

    }
}

