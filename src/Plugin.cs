/***********************************************************************************
MorphTimelineRecorder v0.1 by LFE#9677

Allows recording and playback of morph changes by using a companion empty Atom.

Use Case 1: Record morph changes by directly controlling the Atom for later playback
    - Add the "ADD_ME.cslist" file as a plugin on a Person
    - Open the plugin UI
    - Add any morph for testing
    - NOTE: a HIDDED atom has been added with the morph name in it (make sure
      you select "show hidden" while browsing the atoms in VAM
    - Experiment: drag the Atoms X axis (red arrow direction) and watch the morph change
      the Atom X location is only watched from 0 to 1 in world space
    - To record, arm the empty atom for recording and drag it along the X axis as desired

Use Case 2: Capture changes to morphs indirectly by other plugins
If a morph value changes, the position of the companion Atom will also be set.  In this
way you could in theory record plugin atom movements for later if you want to.

Use Case 3: Use an animation pattern to move the Atom X position, thus changing a linked morph value

CHANGELOG
Version 0.1 2019-12-26
    Initial release.

***********************************************************************************/
using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using System.Linq.Expressions;
using System.Linq;

namespace LFE.MorphTimelineRecorder {
    public class Plugin : MVRScript {

        #region UI elements
        private List<TrackedMorph> TrackedMorphs = new List<TrackedMorph>();
        private JSONStorableFloat TrackingCountJSON;
        private JSONStorableStringChooser NewMorphFilterJSON;
        private JSONStorableStringChooser NewMorphNameJSON;
        #endregion UI elements

        #region constants and naming
        private const string NONE = "None";
        private const string ALL = "All";

        private string NewMorphFilterKey = "morphFilter";
        private string NewMorphNameKey = "newMorph";

        private string TrackedMorphCountKey = "trackingCount";
        private string TrackedMorphAtomNameKey(int i) => $"morph.{i}.atom";
        private string TrackedMorphNameKey(int i) => $"morph.{i}.morph";
        private string TrackedMorphEnabledKey(int i) => $"morph.{i}.enabled";
        private string TrackedMorphGeneratedAtomName(int i, string morphName)
        {
            if(morphName == null || morphName == String.Empty || morphName == NONE)
            {
                return String.Empty;
            }
            else
            {
                return $"MorphAnim{i} {morphName}";
            }
        }
        #endregion constants and naming

        private void UICreateMorphRow(int i, string morphName, bool morphEnabled, string atomName)
        {

            if(morphName == null || morphName == String.Empty || morphName == NONE)
            {
                return;
            }

            if(atomName == null || atomName == String.Empty)
            {
                return;
            }


            DAZMorph morph = MorphController.GetMorphByDisplayName(morphName);
            if(morph == null)
            {
                SuperController.LogError($"Unable to find morph by name {morphName}", false);
                return;
            }

            Atom atom = SuperController.singleton.GetAtomByUid(atomName);
            if (atom == null)
            {
                SuperController.LogError($"Unable to find atom by name {atomName}", false);
                return;
            }

            bool onRightSide = (i % 2 == 1);

            // at this point we are guaranteeed to have a morph / atom object
            var trackedMorph = new TrackedMorph(morph, atom, 0, morphEnabled);

            // invisible atom name (storage in json)
            var a = new JSONStorableString(TrackedMorphAtomNameKey(i), String.Empty);
            a.storeType = JSONStorableParam.StoreType.Full;
            RegisterString(a);
            a.val = trackedMorph.Atom.name;

            // invisible morph name (storage in json)
            var ms = new JSONStorableString(TrackedMorphNameKey(i), String.Empty);
            ms.storeType = JSONStorableParam.StoreType.Full;
            RegisterString(ms);
            ms.val = trackedMorph.Morph.displayName; // set to non-default to force json save

            // invisible morph enabled (storage in json)
            var me = new JSONStorableBool(TrackedMorphEnabledKey(i), !trackedMorph.Enabled);
            me.storeType = JSONStorableParam.StoreType.Full;
            RegisterBool(me);
            me.val = trackedMorph.Enabled ; // set to non-default to force json save

            // the visible checkbox (not tracked in json)
            var morphId = $"Record / Play {trackedMorph.Morph.displayName}";
            var m = new JSONStorableBool(morphId, false, (bool value) =>
            {
                // update the hidden variable for tracking
                me.val = value;
                // update the tracked morph
                trackedMorph.Enabled = value;
            });
            m.storeType = JSONStorableParam.StoreType.Full;
            CreateToggle(m, onRightSide);
            m.val = trackedMorph.Enabled;

            TrackedMorphs.Add(trackedMorph);
            TrackingCountJSON.val += 1;
        }

        private void UICreateAddForm()
        {
            var groupNames = MorphController.GetMorphs().OrderBy(m => m.group.ToLowerInvariant()).Select(m => m.group).Distinct().ToList();
            NewMorphFilterJSON = new JSONStorableStringChooser(NewMorphFilterKey,
                groupNames,
                ALL,
                "Filter Morphs",
                (string filter) =>
                {
                    NewMorphNameJSON.val = String.Empty;
                    NewMorphNameJSON.choices = UIMorphNames;
                });
            CreateFilterablePopup(NewMorphFilterJSON);

            // the exact morph to add
            NewMorphNameJSON = new JSONStorableStringChooser(NewMorphNameKey, UIMorphNames, String.Empty, "Morph");
            var p = CreateFilterablePopup(NewMorphNameJSON, rightSide: true);
            p.popupPanelHeight = 1100f;


            // left hand side arm for record
            var armButton = CreateButton("Arm selected morphs for record");
            armButton.button.onClick.AddListener(() => {
                foreach(var tracked in TrackedMorphs)
                {
                    var motionAnimationControl = tracked.Atom.GetComponentInChildren<MotionAnimationControl>();
                    if(motionAnimationControl == null) {
                        continue;
                    }

                    if(!tracked.Enabled)
                    {
                        SuperController.LogMessage($"un-arming {tracked.Atom.name} for record");
                        motionAnimationControl.armedForRecord = false;
                    }
                    else {
                        SuperController.LogMessage($"arming {tracked.Atom.name} for record");
                        motionAnimationControl.armedForRecord = true;
                    }
                }

            });

            // "add" button
            var b = CreateButton("Add morph...", rightSide: true);
            b.button.onClick.AddListener(() =>
            {
                int i = (int)TrackingCountJSON.val;
                var morphName = NewMorphNameJSON.val ?? String.Empty;

                var generatedAtomName = TrackedMorphGeneratedAtomName(i, morphName);
                StartCoroutine(GetOrCreateEmptyAtom(generatedAtomName, (atom) =>
                {
                    if(atom != null)
                    {
                        UICreateMorphRow(i, morphName, true, atom.name);
                    }
                }));
            });

            // hidden form element to track how many morphs have been added
            TrackingCountJSON = new JSONStorableFloat(TrackedMorphCountKey, 0, 0, 1000);
            TrackingCountJSON.storeType = JSONStorableParam.StoreType.Full;
            RegisterFloat(TrackingCountJSON);



        }

        protected IEnumerator GetOrCreateEmptyAtom(string name, Action<Atom> onCreated)
        {
            if(name == String.Empty)
            {
                onCreated(null);
            }
            else
            {
                Atom atom = SuperController.singleton.GetAtomByUid(name);
                if(atom == null)
                {
                    yield return SuperController.singleton.AddAtomByType("Empty", name);
                    atom = SuperController.singleton.GetAtomByUid(name);
                }

                if(atom != null)
                {
                    atom.hidden = true;
                }
                onCreated(atom);
            }
        }

        public override void RestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, JSONArray presetAtoms = null, bool setMissingToDefault = true)
        {
            if (jc.HasKey(TrackedMorphCountKey))
            {
                for (var i = 0; i < jc[TrackedMorphCountKey].AsInt; i++)
                {
                    string morphEnabledKey = TrackedMorphEnabledKey(i);

                    string morphName = jc[TrackedMorphNameKey(i)] ?? String.Empty;
                    bool morphEnabled = jc.HasKey(morphEnabledKey) ? jc[morphEnabledKey].AsBool : false;
                    string morphAtom = jc[TrackedMorphAtomNameKey(i)] ?? String.Empty;

                    UICreateMorphRow(i, morphName, morphEnabled, morphAtom);
                }
            }

            base.RestoreFromJSON(jc, restorePhysical, restoreAppearance, presetAtoms);
        }

        public override void Init() {
            try {
                TrackedMorphs = new List<TrackedMorph>();
                UICreateAddForm();
            }
            catch (Exception e) {
                SuperController.LogError("Exception caught: " + e);
            }
        }

        void Update() {
            try {
                foreach(var tracked in TrackedMorphs)
                {
                    if(!tracked.Enabled)
                    {
                        continue;
                    }
                    tracked.Sync();
                }
            }
            catch (Exception e) {
                SuperController.LogError("Exception caught: " + e);
            }
        }

        private GenerateDAZMorphsControlUI _morphControlUI;
        private GenerateDAZMorphsControlUI MorphController
        {
            get
            {
                if (_morphControlUI == null)
                {
                    var person = containingAtom;
                    JSONStorable js = person.GetStorableByID("geometry");
                    var dcs = js as DAZCharacterSelector;
                    _morphControlUI = dcs.morphsControlUI;
                }
                return _morphControlUI;
            }
        }

        private List<string> UIMorphNames
        {
            get
            {
                var names = new List<string>();
                var filter = NewMorphFilterJSON.val;
                var morphController = MorphController;

                switch (filter)
                {
                    case ALL:
                        names = morphController.GetMorphDisplayNames();
                        break;
                    default:
                        names = morphController.GetMorphs()
                            .Where(m => m.group.Equals(filter, StringComparison.InvariantCultureIgnoreCase))
                            .Select(m => m.resolvedDisplayName)
                            .ToList();
                        break;
                }
                names.Sort(StringComparer.OrdinalIgnoreCase);

                return names;
            }
        }
    }
}