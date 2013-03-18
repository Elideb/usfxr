using UnityEngine;

public class SfxrSynth {

	/**
	 * SfxrSynth
	 *
	 * Copyright 2013 Thomas Vian, Zeh Fernando
	 *
	 * Licensed under the Apache License, Version 2.0 (the "License");
	 * you may not use this file except in compliance with the License.
	 * You may obtain a copy of the License at
	 *
	 * 	http://www.apache.org/licenses/LICENSE-2.0
	 *
	 * Unless required by applicable law or agreed to in writing, software
	 * distributed under the License is distributed on an "AS IS" BASIS,
	 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	 * See the License for the specific language governing permissions and
	 * limitations under the License.
	 *
	 */

	/**
	 * @author Zeh Fernando
	 */

	//--------------------------------------------------------------------------
	//
	//  Sound Parameters
	//
	//--------------------------------------------------------------------------

	private SfxrParams	_params = new SfxrParams();		// Params instance

	private var _sound:Sound;							// Sound instance used to play the sound
	private var _channel:SoundChannel;					// SoundChannel instance of playing Sound

	private bool	_mutation;							// If the current sound playing or caching is a mutation

	private float[]		_cachedWave;					// Cached wave data from a cacheSound() call
	private bool		_cachingNormal;					// If the synth is caching a normal sound

	private int			_cachingMutation;				// Current caching ID
	private float[]		_cachedMutation;				// Current caching wave data for mutation
	private float[][]	_cachedMutations;				// Cached mutated wave data
	private uint		_cachedMutationsNum;				// Number of cached mutations
	private float		_cachedMutationAmount;			// Amount to mutate during cache

	private bool		_cachingAsync;					// If the synth is currently caching asynchronously
	private uint		_cacheTimePerFrame;				// Maximum time allowed per frame to cache sound asynchronously
	private var _cachedCallback:Function;				// Function to call when finished caching asynchronously
	private var _cacheTicker:Shape;						// Shape used for enterFrame event

	private float[]		_waveData;						// Full wave, read out in chuncks by the onSampleData method
	private uint		_waveDataPos;					// Current position in the waveData
	private uint		_waveDataLength;				// Number of bytes in the waveData
	private uint		_waveDataBytes;					// Number of bytes to write to the soundcard

	private SfxrParams	_original;						// Copied properties for mutation base

	//--------------------------------------------------------------------------
	//
	//  Synth Variables
	//
	//--------------------------------------------------------------------------

	private bool	_finished;							// If the sound has finished

	private float	_masterVolume;						// masterVolume * masterVolume (for quick calculations)

	private uint	_waveType;								// The type of wave to generate

	private float	_envelopeVolume;					// Current volume of the envelope
	private int		_envelopeStage;						// Current stage of the envelope (attack, sustain, decay, end)
	private float	_envelopeTime;						// Current time through current enelope stage
	private float	_envelopeLength;					// Length of the current envelope stage
	private float	_envelopeLength0;					// Length of the attack stage
	private float	_envelopeLength1;					// Length of the sustain stage
	private float	_envelopeLength2;					// Length of the decay stage
	private float	_envelopeOverLength0;				// 1 / _envelopeLength0 (for quick calculations)
	private float	_envelopeOverLength1;				// 1 / _envelopeLength1 (for quick calculations)
	private float	_envelopeOverLength2;				// 1 / _envelopeLength2 (for quick calculations)
	private float	_envelopeFullLength;				// Full length of the volume envelop (and therefore sound)

	private float	_sustainPunch;						// The punch factor (louder at begining of sustain)

	private int		_phase;								// Phase through the wave
	private float	_pos;								// Phase expresed as a Number from 0-1, used for fast sin approx
	private float	_period;							// Period of the wave
	private float	_periodTemp;						// Period modified by vibrato
	private float	_maxPeriod;							// Maximum period before sound stops (from minFrequency)

	private float	_slide;								// Note slide
	private float	_deltaSlide;						// Change in slide
	private float	_minFreqency;						// Minimum frequency before stopping

	private float	_vibratoPhase;						// Phase through the vibrato sine wave
	private float	_vibratoSpeed;						// Speed at which the vibrato phase moves
	private float	_vibratoAmplitude;					// Amount to change the period of the wave by at the peak of the vibrato wave

	private float	_changeAmount;						// Amount to change the note by
	private int		_changeTime;						// Counter for the note change
	private int		_changeLimit;						// Once the time reaches this limit, the note changes

	private float	_squareDuty;						// Offset of center switching point in the square wave
	private float	_dutySweep;							// Amount to change the duty by

	private int		_repeatTime;						// Counter for the repeats
	private int		_repeatLimit;						// Once the time reaches this limit, some of the variables are reset

	private bool	_phaser;							// If the phaser is active
	private float	_phaserOffset;						// Phase offset for phaser effect
	private float	_phaserDeltaOffset;					// Change in phase offset
	private int		_phaserInt;							// Integer phaser offset, for bit maths
	private int		_phaserPos;							// Position through the phaser buffer
	private float[]	_phaserBuffer;						// Buffer of wave values used to create the out of phase second wave

	private bool	_filters;							// If the filters are active
	private float	_lpFilterPos;						// Adjusted wave position after low-pass filter
	private float	_lpFilterOldPos;					// Previous low-pass wave position
	private float	_lpFilterDeltaPos;					// Change in low-pass wave position, as allowed by the cutoff and damping
	private float	_lpFilterCutoff;					// Cutoff multiplier which adjusts the amount the wave position can move
	private float	_lpFilterDeltaCutoff;				// Speed of the low-pass cutoff multiplier
	private float	_lpFilterDamping;					// Damping muliplier which restricts how fast the wave position can move
	private bool	_lpFilterOn;						// If the low pass filter is active

	private float	_hpFilterPos;						// Adjusted wave position after high-pass filter
	private float	_hpFilterCutoff;					// Cutoff multiplier which adjusts the amount the wave position can move
	private float	_hpFilterDeltaCutoff;				// Speed of the high-pass cutoff multiplier

	private float[]	_noiseBuffer;						// Buffer of random values used to generate noise

	private float	_superSample;						// Actual sample writen to the wave
	private float	_sample;							// Sub-sample calculated 8 times per actual sample, averaged out to get the super sample
	private uint	_sampleCount;						// Number of samples added to the buffer sample
	private float	_bufferSample;						// Another supersample used to create a 22050Hz wave

	//--------------------------------------------------------------------------
	//
	//  Getters / Setters
	//
	//--------------------------------------------------------------------------

	/** The sound parameters */
	public SfxrParams paramss {
		get { return _params; }
		set { _params = value; _params.paramsDirty = true; }
	}

	//--------------------------------------------------------------------------
	//
	//  Sound Methods
	//
	//--------------------------------------------------------------------------

	/**
	 * Plays the sound. If the parameters are dirty, synthesises sound as it plays, caching it for later.
	 * If they're not, plays from the cached sound.
	 * Won't play if caching asynchronously.
	 */
	public void play() {
		if (_cachingAsync) return;
	
		stop();
	
		_mutation = false;
	
		if (_params.paramsDirty || _cachingNormal || !_cachedWave) {
			// Needs to cache new data
			_cachedWave = new float[24576];
			_cachingNormal = true;
			_waveData = null;
		
			reset(true);
		} else {
			// Play from cached data
			_waveData = _cachedWave;
			_waveData.position = 0;
			_waveDataLength = _waveData.Length;
			_waveDataBytes = 24576;
			_waveDataPos = 0;
		}
	
		if (!_sound) (_sound = new Sound()).addEventListener(SampleDataEvent.SAMPLE_DATA, onSampleData);
	
		_channel = _sound.play();
	}

	/**
	 * Plays a mutation of the sound.  If the parameters are dirty, synthesises sound as it plays, caching it for later.
	 * If they're not, plays from the cached sound.
	 * Won't play if caching asynchronously.
	 * @param	mutationAmount	Amount of mutation
	 * @param	mutationsNum	The number of mutations to cache before picking from them
	 */
	public void playMutated(float mutationAmount = 0.05f, uint mutationsNum = 15) {
		stop();
	
		if (_cachingAsync) return;
	
		_mutation = true;
	
		_cachedMutationsNum = mutationsNum;
	
		if (_params.paramsDirty || !_cachedMutations) {
			// New set of mutations
			_cachedMutations = new float[_cachedMutationsNum][];
			_cachingMutation = 0;
		}
	
		if (_cachingMutation != -1) {
			// Continuing caching new mutations
			_cachedMutation = new float[24576];
			_cachedMutations[_cachingMutation] = _cachedMutation;
			_waveData = null;
		
			_original = _params.clone();
			_params.mutate(mutationAmount);
			reset(true);
		} else {
			// Play from random cached mutation
			_waveData = _cachedMutations[(uint)(_cachedMutations.Length * Random.value)];
			_waveData.position = 0;
			_waveDataLength = _waveData.Length;
			_waveDataBytes = 24576;
			_waveDataPos = 0;
		}
	
		if (!_sound) (_sound = new Sound()).addEventListener(SampleDataEvent.SAMPLE_DATA, onSampleData);
	
		_channel = _sound.play();
	}

	/**
	 * Stops the currently playing sound
	 */
	public void stop() {
		if (_channel) {
			_channel.stop();
			_channel = null;
		}
	
		if (_original != null) {
			_params.copyFrom(_original);
			_original = null;
		}
	}

	/**
	 * If there is a cached sound to play, reads out of the data.
	 * If there isn't, synthesises new chunch of data, caching it as it goes.
	 * @param	e	SampleDataEvent to write data to
	 */
	private void onSampleData(SampleDataEvent e) {
		if (_waveData) {
			if (_waveDataPos + _waveDataBytes > _waveDataLength) _waveDataBytes = _waveDataLength - _waveDataPos;
		
			if (_waveDataBytes > 0) e.data.writeBytes(_waveData, _waveDataPos, _waveDataBytes);
		
			_waveDataPos += _waveDataBytes;
		} else {
			uint length;
			uint i, l;
		
			if (_mutation) {
				if (_original != null) {
					_waveDataPos = _cachedMutation.position;
				
					if (synthWave(_cachedMutation, 3072, true)) {
						_params.copyFrom(_original);
						_original = null;
					
						_cachingMutation++;
					
						if ((length = _cachedMutation.Length) < 24576) {
							// If the sound is smaller than the buffer length, add silence to allow it to play
							_cachedMutation.position = length;
							for (i = 0, l = 24576 - length; i < l; i++) _cachedMutation.writeFloat(0.0);
						}
					
						if (_cachingMutation >= _cachedMutationsNum) {
							_cachingMutation = -1;
						}
					}
				
					_waveDataBytes = _cachedMutation.Length - _waveDataPos;
				
					e.data.writeBytes(_cachedMutation, _waveDataPos, _waveDataBytes);
				}
			} else {
				if (_cachingNormal) {
					_waveDataPos = _cachedWave.position;
				
					if (synthWave(_cachedWave, 3072, true)) {
						if ((length = _cachedWave.Length) < 24576) {
							// If the sound is smaller than the buffer length, add silence to allow it to play
							_cachedWave.position = length;
							for (i = 0, l = 24576 - length; i < l; i++) _cachedWave.writeFloat(0.0f);
						}
					
						_cachingNormal = false;
					}
				
					_waveDataBytes = _cachedWave.Length - _waveDataPos;
				
					e.data.writeBytes(_cachedWave, _waveDataPos, _waveDataBytes);
				}
			}
		}
	}

	//--------------------------------------------------------------------------
	//
	//  Cached Sound Methods
	//
	//--------------------------------------------------------------------------

	/**
	 * Cache the sound for speedy playback.
	 * If a callback is passed in, the caching will be done asynchronously, taking maxTimePerFrame milliseconds
	 * per frame to cache, them calling the callback when it's done.
	 * If not, the whole sound is cached imidiately - can freeze the player for a few seconds, especially in debug mode.
	 * @param	callback			Function to call when the caching is complete
	 * @param	maxTimePerFrame		Maximum time in milliseconds the caching will use per frame
	 */
	public void cacheSound(Function callback = null, uint maxTimePerFrame = 5) {
		stop();
	
		if (_cachingAsync) return;
	
		reset(true);
	
		_cachedWave = new float[24576];
	
		if (Boolean(callback)) {
			_mutation = false;
			_cachingNormal = true;
			_cachingAsync = true;
			_cacheTimePerFrame = maxTimePerFrame;
		
			_cachedCallback = callback;
		
			if (!_cacheTicker) _cacheTicker = new Shape;
		
			_cacheTicker.addEventListener(Event.ENTER_FRAME, cacheSection);
		} else {
			_cachingNormal = false;
			_cachingAsync = false;
		
			synthWave(_cachedWave, _envelopeFullLength, true);
		
			/*
			// Disabled as unnecessary --zeh
			uint length = _cachedWave.Length;
		
			if (length < 24576) {
				// If the sound is smaller than the buffer length, add silence to allow it to play
				_cachedWave.position = length;
				for (uint i = 0, l = 24576 - length; i < l; i++) _cachedWave.writeFloat(0.0f);
			}
			*/
		}
	}

	/**
	 * Caches a series of mutations on the source sound.
	 * If a callback is passed in, the caching will be done asynchronously, taking maxTimePerFrame milliseconds
	 * per frame to cache, them calling the callback when it's done.
	 * If not, the whole sound is cached imidiately - can freeze the player for a few seconds, especially in debug mode.
	 * @param	mutationsNum		Number of mutations to cache
	 * @param	mutationAmount		Amount of mutation
	 * @param	callback			Function to call when the caching is complete
	 * @param	maxTimePerFrame		Maximum time in milliseconds the caching will use per frame
	 */
	public void cacheMutations(uint mutationsNum, float mutationAmount = 0.05f, Function callback = null, uint maxTimePerFrame = 5) {
		stop();
	
		if (_cachingAsync) return;
	
		_cachedMutationsNum = mutationsNum;
		_cachedMutations = new float[_cachedMutationsNum];
	
		if (callback != null) {
			_mutation = true;
		
			_cachingMutation = 0;
			_cachedMutation = new float[24576];
			_cachedMutations[0] = _cachedMutation;
			_cachedMutationAmount = mutationAmount;
		
			_original = _params.clone();
			_params.mutate(mutationAmount);
		
			reset(true);
		
			_cachingAsync = true;
			_cacheTimePerFrame = maxTimePerFrame;
		
			_cachedCallback = callback;
		
			if (!_cacheTicker) _cacheTicker = new Shape;
		
			_cacheTicker.addEventListener(Event.ENTER_FRAME, cacheSection);
		} else {
			SfxrParams original = _params.clone();
		
			for (uint i = 0; i < _cachedMutationsNum; i++) {
				_params.mutate(mutationAmount);
				cacheSound();
				_cachedMutations[i] = _cachedWave;
				_params.copyFrom(original);
			}
		
			_cachingMutation = -1;
		}
	}

	/**
	 * Performs the asynchronous cache, working for up to _cacheTimePerFrame milliseconds per frame
	 * @param	e	enterFrame event
	 */
	private void cacheSection(Event e) {
		uint cacheStartTime = getTimer();
	
		while (getTimer() - cacheStartTime < _cacheTimePerFrame) {
			if (_mutation) {
				_waveDataPos = _cachedMutation.position;
			
				if (synthWave(_cachedMutation, 500, true)) {
					_params.copyFrom(_original);
					_params.mutate(_cachedMutationAmount);
					reset(true);
				
					_cachingMutation++;
					_cachedMutation = new ByteArray;
					_cachedMutations[_cachingMutation] = _cachedMutation;
				
					if (_cachingMutation >= _cachedMutationsNum) {
						_cachingMutation = -1;
						_cachingAsync = false;
					
						_params.paramsDirty = false;
					
						_cachedCallback();
						_cachedCallback = null;
						_cacheTicker.removeEventListener(Event.ENTER_FRAME, cacheSection);
					
						return;
					}
				}
			} else {
				_waveDataPos = _cachedWave.position;
			
				if (synthWave(_cachedWave, 500, true)) {
					_cachingNormal = false;
					_cachingAsync = false;
				
					_cachedCallback();
					_cachedCallback = null;
					_cacheTicker.removeEventListener(Event.ENTER_FRAME, cacheSection);
				
					return;
				}
			}
		}
	}

	//--------------------------------------------------------------------------
	//
	//  Synth Methods
	//
	//--------------------------------------------------------------------------

	/**
	 * Resets the runing variables from the params
	 * Used once at the start (total reset) and for the repeat effect (partial reset)
	 * @param	totalReset	If the reset is total
	 */
	private void reset(bool totalReset) {
		// Shorter reference
		SfxrParams p = _params;
	
		_period = 100.0f / (p.startFrequency * p.startFrequency + 0.001f);
		_maxPeriod = 100.0f / (p.minFrequency * p.minFrequency + 0.001f);
	
		_slide = 1.0f - p.slide * p.slide * p.slide * 0.01f;
		_deltaSlide = -p.deltaSlide * p.deltaSlide * p.deltaSlide * 0.000001f;
	
		if (p.waveType == 0) {
			_squareDuty = 0.5f - p.squareDuty * 0.5f;
			_dutySweep = -p.dutySweep * 0.00005f;
		}
	
		if (p.changeAmount > 0.0) 	_changeAmount = 1.0f - p.changeAmount * p.changeAmount * 0.9f;
		else 						_changeAmount = 1.0f + p.changeAmount * p.changeAmount * 10.0f;
	
		_changeTime = 0;
	
		if (p.changeSpeed == 1.0f) 	_changeLimit = 0;
		else 						_changeLimit = (int)((1f - p.changeSpeed) * (1f - p.changeSpeed) * 20000f + 32f);
	
		if (totalReset) {
			p.paramsDirty = false;
		
			_masterVolume = p.masterVolume * p.masterVolume;
		
			_waveType = p.waveType;
		
			if (p.sustainTime < 0.01) p.sustainTime = 0.01f;
		
			float totalTime = p.attackTime + p.sustainTime + p.decayTime;
			if (totalTime < 0.18f) {
				float multiplier = 0.18f / totalTime;
				p.attackTime *= multiplier;
				p.sustainTime *= multiplier;
				p.decayTime *= multiplier;
			}
		
			_sustainPunch = p.sustainPunch;
		
			_phase = 0;
		
			_minFreqency = p.minFrequency;
		
			_filters = p.lpFilterCutoff != 1.0 || p.hpFilterCutoff != 0.0;
		
			_lpFilterPos = 0.0f;
			_lpFilterDeltaPos = 0.0f;
			_lpFilterCutoff = p.lpFilterCutoff * p.lpFilterCutoff * p.lpFilterCutoff * 0.1f;
			_lpFilterDeltaCutoff = 1.0f + p.lpFilterCutoffSweep * 0.0001f;
			_lpFilterDamping = 5.0f / (1.0f + p.lpFilterResonance * p.lpFilterResonance * 20.0f) * (0.01f + _lpFilterCutoff);
			if (_lpFilterDamping > 0.8f) _lpFilterDamping = 0.8f;
			_lpFilterDamping = 1.0f - _lpFilterDamping;
			_lpFilterOn = p.lpFilterCutoff != 1.0f;
		
			_hpFilterPos = 0.0f;
			_hpFilterCutoff = p.hpFilterCutoff * p.hpFilterCutoff * 0.1f;
			_hpFilterDeltaCutoff = 1.0f + p.hpFilterCutoffSweep * 0.0003f;
		
			_vibratoPhase = 0.0f;
			_vibratoSpeed = p.vibratoSpeed * p.vibratoSpeed * 0.01f;
			_vibratoAmplitude = p.vibratoDepth * 0.5f;
		
			_envelopeVolume = 0.0f;
			_envelopeStage = 0;
			_envelopeTime = 0;
			_envelopeLength0 = p.attackTime * p.attackTime * 100000.0f;
			_envelopeLength1 = p.sustainTime * p.sustainTime * 100000.0f;
			_envelopeLength2 = p.decayTime * p.decayTime * 100000.0f + 10f;
			_envelopeLength = _envelopeLength0;
			_envelopeFullLength = _envelopeLength0 + _envelopeLength1 + _envelopeLength2;
		
			_envelopeOverLength0 = 1.0f / _envelopeLength0;
			_envelopeOverLength1 = 1.0f / _envelopeLength1;
			_envelopeOverLength2 = 1.0f / _envelopeLength2;
		
			_phaser = p.phaserOffset != 0.0f || p.phaserSweep != 0.0f;
		
			_phaserOffset = p.phaserOffset * p.phaserOffset * 1020.0f;
			if (p.phaserOffset < 0.0f) _phaserOffset = -_phaserOffset;
			_phaserDeltaOffset = p.phaserSweep * p.phaserSweep * p.phaserSweep * 0.2f;
			_phaserPos = 0;
		
			if (!_phaserBuffer) _phaserBuffer = new float[1024];
			if (!_noiseBuffer) _noiseBuffer = new float[32];
		
			uint i;
			for (i = 0; i < 1024; i++) _phaserBuffer[i] = 0.0;
			for (i = 0; i < 32; i++) _noiseBuffer[i] = Random.value * 2.0f - 1.0f;
		
			_repeatTime = 0;
		
			if (p.repeatSpeed == 0.0) {
				_repeatLimit = 0;
			} else {
				_repeatLimit = (int)((1.0-p.repeatSpeed) * (1.0-p.repeatSpeed) * 20000) + 32;
			}
		}
	}

	/**
	 * Writes the wave to the supplied buffer ByteArray
	 * @param	buffer		A ByteArray to write the wave to
	 * @param	waveData	If the wave should be written for the waveData
	 * @return				If the wave is finished
	 */
	private bool synthWave(ByteArray buffer, uint length, bool waveData = false, uint sampleRate = 44100, uint bitDepth = 16) {
		_finished = false;
	
		_sampleCount = 0;
		_bufferSample = 0.0f;
	
		for (uint i = 0; i < length; i++) {
			if (_finished) return true;
		
			// Repeats every _repeatLimit times, partially resetting the sound parameters
			if (_repeatLimit != 0) {
				if (++_repeatTime >= _repeatLimit) {
					_repeatTime = 0;
					reset(false);
				}
			}
		
			// If _changeLimit is reached, shifts the pitch
			if (_changeLimit != 0) {
				if (++_changeTime >= _changeLimit) {
					_changeLimit = 0;
					_period *= _changeAmount;
				}
			}
		
			// Acccelerate and apply slide
			_slide += _deltaSlide;
			_period *= _slide;
		
			// Checks for frequency getting too low, and stops the sound if a minFrequency was set
			if (_period > _maxPeriod) {
				_period = _maxPeriod;
				if (_minFreqency > 0.0) _finished = true;
			}
		
			_periodTemp = _period;
		
			// Applies the vibrato effect
			if (_vibratoAmplitude > 0.0) {
				_vibratoPhase += _vibratoSpeed;
				_periodTemp = _period * (1.0f + Mathf.Sin(_vibratoPhase) * _vibratoAmplitude);
			}
		
			_periodTemp = int(_periodTemp);
			if (_periodTemp < 8) _periodTemp = 8;
		
			// Sweeps the square duty
			if (_waveType == 0) {
				_squareDuty += _dutySweep;
						if (_squareDuty < 0.0) _squareDuty = 0.0f;
				else if (_squareDuty > 0.5) _squareDuty = 0.5f;
			}
		
			// Moves through the different stages of the volume envelope
			if (++_envelopeTime > _envelopeLength) {
				_envelopeTime = 0;
			
				switch(++_envelopeStage) {
					case 1: _envelopeLength = _envelopeLength1; break;
					case 2: _envelopeLength = _envelopeLength2; break;
				}
			}
		
			// Sets the volume based on the position in the envelope
			switch(_envelopeStage) {
				case 0: _envelopeVolume = _envelopeTime * _envelopeOverLength0; 										break;
				case 1: _envelopeVolume = 1.0f + (1.0f - _envelopeTime * _envelopeOverLength1) * 2.0f * _sustainPunch;	break;
				case 2: _envelopeVolume = 1.0f - _envelopeTime * _envelopeOverLength2; 									break;
				case 3: _envelopeVolume = 0.0f; _finished = true; 														break;
			}
		
			// Moves the phaser offset
			if (_phaser) {
				_phaserOffset += _phaserDeltaOffset;
				_phaserInt = (int)_phaserOffset;
				if (_phaserInt < 0) {
					_phaserInt = -_phaserInt;
				} else if (_phaserInt > 1023) {
					_phaserInt = 1023;
				}
			}
		
			// Moves the high-pass filter cutoff
			if (_filters && _hpFilterDeltaCutoff != 0.0) {
				_hpFilterCutoff *= _hpFilterDeltaCutoff;
				if (_hpFilterCutoff < 0.00001f) {
					_hpFilterCutoff = 0.00001f;
				} else if (_hpFilterCutoff > 0.1) {
					_hpFilterCutoff = 0.1f;
				}
			}
		
			_superSample = 0.0f;
			for (int j = 0; j < 8; j++) {
				// Cycles through the period
				_phase++;
				if (_phase >= _periodTemp) {
					_phase = _phase - (int)_periodTemp;
				
					// Generates new random noise for this period
					if (_waveType == 3) {
						for (uint n = 0; n < 32; n++) _noiseBuffer[n] = Random.value * 2.0f - 1.0f;
					}
				}
			
				// Gets the sample from the oscillator
				switch(_waveType) {
					case 0: // Square wave
						_sample = ((_phase / _periodTemp) < _squareDuty) ? 0.5f : -0.5f;
						break;
					case 1: // Saw wave
						_sample = 1.0f - (_phase / _periodTemp) * 2.0f;
						break;
					case 2: // Sine wave (fast and accurate approx) {
						_pos = _phase / _periodTemp;
						_pos = _pos > 0.5f ? (_pos - 1.0f) * 6.28318531f : _pos * 6.28318531f;
						_sample = _pos < 0 ? 1.27323954f * _pos + 0.405284735f * _pos * _pos : 1.27323954f * _pos - 0.405284735f * _pos * _pos;
						_sample = _sample < 0 ? 0.225f * (_sample *-_sample - _sample) + _sample : 0.225f * (_sample * _sample - _sample) + _sample;
						break;
					case 3: // Noise
						_sample = _noiseBuffer[(uint)(_phase * 32 / (int)_periodTemp)];
						break;
				}
			
				// Applies the low and high pass filters
				if (_filters) {
					_lpFilterOldPos = _lpFilterPos;
					_lpFilterCutoff *= _lpFilterDeltaCutoff;
					if (_lpFilterCutoff < 0.0) {
						_lpFilterCutoff = 0.0f;
					} else if (_lpFilterCutoff > 0.1) {
						_lpFilterCutoff = 0.1f;
					}
				
					if (_lpFilterOn) {
						_lpFilterDeltaPos += (_sample - _lpFilterPos) * _lpFilterCutoff;
						_lpFilterDeltaPos *= _lpFilterDamping;
					} else {
						_lpFilterPos = _sample;
						_lpFilterDeltaPos = 0.0f;
					}
				
					_lpFilterPos += _lpFilterDeltaPos;
				
					_hpFilterPos += _lpFilterPos - _lpFilterOldPos;
					_hpFilterPos *= 1.0f - _hpFilterCutoff;
					_sample = _hpFilterPos;
				}
			
				// Applies the phaser effect
				if (_phaser) {
					_phaserBuffer[_phaserPos&1023] = _sample;
					_sample += _phaserBuffer[(_phaserPos - _phaserInt + 1024) & 1023];
					_phaserPos = (_phaserPos + 1) & 1023;
				}
			
				_superSample += _sample;
			}
		
			// Averages out the super samples and applies volumes
			_superSample = _masterVolume * _envelopeVolume * _superSample * 0.125f;
		
			// Clipping if too loud
			if (_superSample > 1.0f) {
				_superSample = 1.0f;
			} else if (_superSample < -1.0f) {
				_superSample = -1.0f;
			}
		
			if (waveData) {
				// Writes same value to left and right channels
				buffer.writeFloat(_superSample);
				buffer.writeFloat(_superSample);
			} else {
				_bufferSample += _superSample;
			
				_sampleCount++;
			
				// Writes mono wave data to the .wav format
				if (sampleRate == 44100 || _sampleCount == 2) {
					_bufferSample /= _sampleCount;
					_sampleCount = 0;
				
					if (bitDepth == 16) {
						buffer.writeShort((int)(32000.0 * _bufferSample));
					} else {
						buffer.writeByte(_bufferSample * 127 + 128);
					}
				
					_bufferSample = 0.0f;
				}
			}
		}
	
		return false;
	}


	//--------------------------------------------------------------------------
	//
	//  .wav File Methods
	//
	//--------------------------------------------------------------------------

	/**
	 * Returns a ByteArray of the wave in the form of a .wav file, ready to be saved out
	 * @param	sampleRate		Sample rate to generate the .wav at
	 * @param	bitDepth		Bit depth to generate the .wav at
	 * @return					Wave in a .wav file
	 */
	public ByteArray getWavFile(uint sampleRate = 44100, uint bitDepth = 16) {
		stop();
	
		reset(true);
	
		if (sampleRate != 44100) sampleRate = 22050;
		if (bitDepth != 16) bitDepth = 8;
	
		var soundLength:uint = _envelopeFullLength;
		if (bitDepth == 16) soundLength *= 2;
		if (sampleRate == 22050) soundLength /= 2;
	
		var filesize:int = 36 + soundLength;
		var blockAlign:int = bitDepth / 8;
		var bytesPerSec:int = sampleRate * blockAlign;
	
		var wav:ByteArray = new ByteArray();
	
		// Header
		wav.endian = Endian.BIG_ENDIAN;
		wav.writeUnsignedInt(0x52494646);		// Chunk ID "RIFF"
		wav.endian = Endian.LITTLE_ENDIAN;
		wav.writeUnsignedInt(filesize);			// Chunck Data Size
		wav.endian = Endian.BIG_ENDIAN;
		wav.writeUnsignedInt(0x57415645);		// RIFF Type "WAVE"
	
		// Format Chunk
		wav.endian = Endian.BIG_ENDIAN;
		wav.writeUnsignedInt(0x666D7420);		// Chunk ID "fmt "
		wav.endian = Endian.LITTLE_ENDIAN;
		wav.writeUnsignedInt(16);				// Chunk Data Size
		wav.writeShort(1);						// Compression Code PCM
		wav.writeShort(1);						// Number of channels
		wav.writeUnsignedInt(sampleRate);		// Sample rate
		wav.writeUnsignedInt(bytesPerSec);		// Average bytes per second
		wav.writeShort(blockAlign);				// Block align
		wav.writeShort(bitDepth);				// Significant bits per sample
	
		// Data Chunk
		wav.endian = Endian.BIG_ENDIAN;
		wav.writeUnsignedInt(0x64617461);		// Chunk ID "data"
		wav.endian = Endian.LITTLE_ENDIAN;
		wav.writeUnsignedInt(soundLength);		// Chunk Data Size
	
		synthWave(wav, _envelopeFullLength, false, sampleRate, bitDepth);
	
		wav.position = 0;
	
		return wav;
	}
}