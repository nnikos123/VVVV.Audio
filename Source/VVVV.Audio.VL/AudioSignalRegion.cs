﻿using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Core;
using VL.Lib.Animation;
using VL.Lib.Collections;
using VVVV.Audio;
using VVVV.Audio.Signals;

namespace VL.Lib.VAudio
{
    [Type]
    [Node(OperationsOfProcessNode = "Create, Update", StateTypeParameter = nameof(TState))]
    public class AudioSignalRegion<TState, TIn, TOut> : IDisposable
       where TState : class
    {
        internal BufferCallerSignal PerBufferSignal;

        readonly Subject<TOut> FSubject;
        readonly IObservable<TOut> FResult;

        [Node(Hidden = true)]
        public AudioSignalRegion()
            : this(new BufferCallerSignal())
        {
        }

        public AudioSignalRegion(BufferCallerSignal bufferSignal)
        {
            //signal
            PerBufferSignal = bufferSignal;
            FSubject = new Subject<TOut>();
            FResult = FSubject.Publish().RefCount();
        }

        Func<TState, TIn, AudioBufferStereo, Tuple<TState, TOut>> FUpdater;
        TOut FOutput;

        public TState State
        {
            get;
            internal set;
        }

        [Node(Hidden = true)]
        public Spread<AudioSignal> Update(IEnumerable<AudioSignal> stereoInput, TIn input, bool reset, Func<TState> create, Func<TState, TIn, AudioBufferStereo, Tuple<TState, TOut>> update, out IObservable<TOut> onBufferProcessed, out Time time)
        {
            if (reset || State == null)
                State = create();

            PerBufferSignal.SetInput(stereoInput);

            if (FUpdater != update)
            {
                PerBufferSignal.PerBuffer = (buffer) =>
                {
                    try
                    {
                        var result = FUpdater(State, input, buffer);
                        State = result.Item1;
                        FOutput = result.Item2;
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine(e);
                    }
                };

                FUpdater = update;
            }

            time = PerBufferSignal.StereoBuffer.StartTime;
            
            if(FOutput != null)
                FSubject.OnNext(FOutput);
            onBufferProcessed = FResult;
            return PerBufferSignal.Outputs.ToSpread();
        }

        [Node(Hidden = true)]
        public void Dispose()
        {
            var disposable = State as IDisposable;
            disposable?.Dispose();
            FSubject.Dispose();
        }
    }
}
