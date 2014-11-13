﻿using Sensus.Exceptions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Sensus.Probes
{
    /// <summary>
    /// An abstract probe.
    /// </summary>
    public abstract class Probe : INotifyPropertyChanged
    {
        #region static members
        /// <summary>
        /// Gets a list of all probes, uninitialized and with default parameter values.
        /// </summary>
        /// <returns></returns>
        public static List<Probe> GetAll()
        {
            return Assembly.GetExecutingAssembly().GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(Probe))).Select(t => Activator.CreateInstance(t) as Probe).ToList();
        }
        #endregion

        /// <summary>
        /// Fired when a UI-relevant property is changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private int _id;
        private string _name;
        private bool _enabled;
        private ProbeState _state;
        private HashSet<Datum> _collectedData;

        public int Id
        {
            get { return _id; }
        }

        public string Name
        {
            get { return _name; }
            set
            {
                if (!value.Equals(_name, StringComparison.Ordinal))
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (value != _enabled)
                {
                    _enabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public ProbeState State
        {
            get { return _state; }
        }

        protected abstract string DisplayName { get; }

        public Probe()
        {
            _id = -1;
            _name = DisplayName;
            _enabled = false;
            _state = ProbeState.Uninitialized;
            _collectedData = new HashSet<Datum>();
        }

        public virtual ProbeState Initialize()
        {
            lock (this)
            {
                _state = ProbeState.Initializing;
                _id = 1;  // TODO:  Get probe Id.
                _collectedData.Clear();
            }

            return _state;
        }

        internal void ChangeState(ProbeState requiredCurrentState, ProbeState newState)
        {
            lock (this)
            {
                if (_state != requiredCurrentState)
                    throw new InvalidProbeStateException(this, newState);

                bool stateChanged = _state != newState;

                _state = newState;

                if (stateChanged)
                    OnPropertyChanged("State");
            }
        }

        public abstract void Test();

        public abstract void Start();

        protected void StoreDatum(Datum datum)
        {
            if (datum != null)
                lock (_collectedData)
                    _collectedData.Add(datum);
        }

        public IEnumerable<Datum> GetCollectedData()
        {
            return _collectedData;
        }

        public void ClearCommittedData(IEnumerable<Datum> data)
        {
            lock (_collectedData)
                foreach (Datum d in data)
                    _collectedData.Remove(d);
        }

        public abstract void Stop();

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
