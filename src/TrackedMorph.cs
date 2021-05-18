using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using System.Linq.Expressions;
using System.Linq;

namespace LFE.MorphTimelineRecorder
{
    public class TrackedMorph
    {

        #region properties

        /// <summary>
        /// Get the Atom
        /// </summary>
        public Atom Atom { get; }

        /// <summary>
        /// Get or set the Atom positional value
        /// </summary>
        public float AtomValueActual
        {
            get
            {
                return _atomTransform.position.x;
            }
            set
            {
                _prevAtomValue = AtomValueActual;
                // set the x position - leave others the same
                _atomTransform.position = new Vector3(value, _atomTransform.position.y, _atomTransform.position.z);
            }
        }

        /// <summary>
        /// Get or set the Atom positional value using a percentage (between 0 and 1)
        /// </summary>
        public float AtomValueNormalized
        {
            get
            {
                return Mathf.InverseLerp(0, 1, AtomValueActual);
            }
            set
            {
                AtomValueActual = Mathf.Lerp(0, 1, value);
            }
        }

        /// <summary>
        /// Get the Morph
        /// </summary>
        public DAZMorph Morph { get; }

        /// <summary>
        /// Get or set the Morph value
        /// </summary>
        public float MorphValueActual
        {
            get
            {
                return Morph.morphValue;
            }
            set
            {
                _prevMorphValue = MorphValueActual;
                Morph.SetValue(value);
            }
        }

        /// <summary>
        /// Get or set the Morph value using a percentage (between 0 and 1)
        /// </summary>
        public float MorphValueNormalized
        {
            get
            {
                return Mathf.InverseLerp(Morph.min, Morph.max, MorphValueActual);
            }
            set
            {
                MorphValueActual = Mathf.Lerp(Morph.min, Morph.max, value);
            }
        }

        /// <summary>
        /// Should any changes be synced up at all?
        /// </summary>
        public bool Enabled { get; set; } = true;
        #endregion properties

        private readonly int _controllerIndex;
        private float _prevAtomValue;
        private float _prevMorphValue;
        private Transform _atomTransform => Atom.freeControllers[_controllerIndex].transform;

        /// <summary>
        /// Create an object that tracks a morph and atom pair.
        /// 
        /// Changes to an atom position are synced to a change to a morph value.
        /// 
        /// Changes to a morph value are converted to a positional change to the atom.
        /// </summary>
        /// <param name="morph">The morph for recording and playback</param>
        /// <param name="atom">The atom to store morph changes for recording and playback</param>
        /// <param name="freeControllerIndex">Which <see cref="FreeControllerV3"/> position to consider</param>
        /// <param name="enabled">Should this morph/atom pair be watched for changes anymore?</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public TrackedMorph(DAZMorph morph, Atom atom, int freeControllerIndex, bool enabled = true)
        {
            if(morph == null)
            {
                throw new ArgumentNullException(nameof(morph));
            }

            if(atom == null)
            {
                throw new ArgumentNullException(nameof(atom));
            }

            if (freeControllerIndex < 0 || freeControllerIndex >= atom.freeControllers.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(freeControllerIndex), $"atom has {atom.freeControllers.Length} freecontrollers and you are looking for {freeControllerIndex}");
            }

            Morph = morph;
            Atom = atom;
            Enabled = enabled;

            _controllerIndex = freeControllerIndex;
            _prevAtomValue = AtomValueActual;
            _prevMorphValue = MorphValueActual;
        }


        /// <summary>
        /// Looks for changes to the Atom or Morph since the last time <see cref="Sync"/> was called
        /// and makes sure the Atom positional value proportion matches the Morph value proportion
        /// (and the other way too)
        /// </summary>
        public void Sync()
        {

            if (!Enabled)
            {
                return;
            }

            bool hasAtomChanged = HasAtomChanged();
            bool hasMorphChanged = HasMorphChanged();

            // if both things have changed .. then the atom wins
            if (hasAtomChanged && hasMorphChanged)
            {
                SyncFromAtom();
            }
            else if (hasAtomChanged)
            {
                SyncFromAtom();
            }
            else if (hasMorphChanged)
            {
                SyncFromMorph();
            }
        }

        public void SyncFromMorph() {
            AtomValueNormalized = MorphValueNormalized;
            _prevAtomValue = AtomValueActual;
            _prevMorphValue = MorphValueActual;
        }

        public void SyncFromAtom() {
            MorphValueNormalized = AtomValueNormalized;
            _prevAtomValue = AtomValueActual;
            _prevMorphValue = MorphValueActual;
        }

        /// <summary>
        /// Did the atom value change since the last time sync was called?
        /// </summary>
        public bool HasAtomChanged()
        {
            try
            {
                return _prevAtomValue != AtomValueActual;
            }
            catch (Exception)
            {
                Enabled = false;
                return false;
            }
        }

        /// <summary>
        /// Did the morph value change since the last time sync was called?
        /// </summary>
        public bool HasMorphChanged()
        {
            try
            {
                return _prevMorphValue != MorphValueActual;
            }
            catch (Exception)
            {
                Enabled = false;
                return false;
            }
        }

        /// <summary>
        /// Did the morph or atom values change since the last time sync was called?
        /// </summary>
        public bool HasChanged()
        {
            return HasMorphChanged() || HasAtomChanged();
        }

        public override string ToString()
        {
            try
            {
                return $"Morph[{Morph.displayName}, ({_prevMorphValue}), {MorphValueActual}, {MorphValueNormalized}] Atom[{Atom.name}, ({_prevAtomValue}), {AtomValueActual}, {AtomValueNormalized}]";
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }
    }
}
