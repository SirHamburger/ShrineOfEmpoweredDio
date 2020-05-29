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
    [BepInPlugin("com.MagnusMagnuson.ShrineOfEmpoweredDio", "ShrineOfEmpoweredDio", "1.3.2")]
    public class ShrineOfDio : BaseUnityPlugin
    {

        public static ConfigWrapper<bool> UseBalancedMode;
        public static ConfigWrapper<int> ResurrectionCost;

        public const int UNINITIALIZED = -2;

        public const int BALANCED_MODE = -1;

        public int clientCost = UNINITIALIZED;
        public bool isBalancedMode = true;

        private int useCount = 0;


        public void Awake()
        {
            InitConfig();


            On.RoR2.SceneDirector.PopulateScene += (orig, self) =>
            {
                orig(self);
                SpawnShrineOfDio(self);

            };

            On.RoR2.ShrineHealingBehavior.FixedUpdate += (orig, self) =>
            {
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
                orig(self);

                PurchaseInteraction pi = self.GetFieldValue<PurchaseInteraction>("purchaseInteraction");
                pi.contextToken = "Offer to the Shrine of Dio";
                pi.displayNameToken = "Shrine of Dio";
                self.costMultiplierPerPurchase = 1f;


                isBalancedMode = UseBalancedMode.Value;
                if (UseBalancedMode.Value)
                {
                    pi.costType = CostTypeIndex.None;
                    pi.cost = BALANCED_MODE;
                    pi.GetComponent<HologramProjector>().displayDistance = 0f;
                    self.GetComponent<HologramProjector>().displayDistance = 0f;
                }
                else
                {
                    pi.costType = CostTypeIndex.Money;
                    pi.cost = ResurrectionCost.Value;
                }


            };

            On.RoR2.ShrineHealingBehavior.AddShrineStack += (orig, self, interactor) =>
            {

                string resurrectionMessage = $"<color=#beeca1>{interactor.GetComponent<CharacterBody>().GetUserName()}</color> resurrected <color=#beeca1>Dio Shrine</color>";
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
                if (self.displayNameToken.Contains("Shrine of Dio") || self.displayNameToken.Contains("SHRINE_HEALING"))
                {
                    if (interactor.GetComponent<CharacterBody>().inventory.GetItemCount(ItemIndex.ExtraLifeConsumed) > 0)
                    {
                        return orig(self, interactor);
                    }



                }
                return orig(self, interactor);
            };

        }

        private void InitConfig()
        {
            UseBalancedMode = Config.Wrap(
            "Config",
            "UseBalancedMode",
            "Setting this to true will only allow you to resurrect other players for one of your Dio's Best Friend. Turning this off will allow you to instead use gold.",
            false);

            ResurrectionCost = Config.Wrap(
            "Config",
            "ResurrectionCost",
            "[Only active if you set UseBalancedMode to false] Cost for a resurrection. Scales with difficulty but doesn't increase each usage. Regular Chest cost is 25, Golden/Legendary Chest is 400. Default is 300.",
            300);
        }

        private void UpdateShrineDisplay(ShrineHealingBehavior self)
        {
            PurchaseInteraction pi = self.GetFieldValue<PurchaseInteraction>("purchaseInteraction");
            if (clientCost == BALANCED_MODE)
            {
                pi.costType = CostTypeIndex.None;
                pi.cost = BALANCED_MODE;
                pi.GetComponent<HologramProjector>().displayDistance = 0f;
                self.GetComponent<HologramProjector>().displayDistance = 0f;

            }
            else
            {
                pi.costType = CostTypeIndex.Money;
                pi.cost = clientCost;
            }
        }

        private int GetDifficultyScaledCost(int baseCost)
        {
            return (int)((double)baseCost * (double)Mathf.Pow(Run.instance.difficultyCoefficient, 1.25f)); // 1.25f
        }


        public void SpawnShrineOfDio(SceneDirector self)
        {
            Xoroshiro128Plus xoroshiro128Plus = new Xoroshiro128Plus(self.GetFieldValue<Xoroshiro128Plus>("rng").nextUlong);
            if (SceneInfo.instance.countsAsStage)
            {
                SpawnCard card = Resources.Load<SpawnCard>("SpawnCards/InteractableSpawnCard/iscShrineHealing");

                GameObject gameObject3 = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(card, new DirectorPlacementRule
                {
                    placementMode = DirectorPlacementRule.PlacementMode.Random
                }, xoroshiro128Plus));

                if (!UseBalancedMode.Value)
                {
                    gameObject3.GetComponent<PurchaseInteraction>().Networkcost = GetDifficultyScaledCost(ResurrectionCost.Value);
                }
            }
        }

    }
}

