﻿using System;
using System.Collections.Generic;
using FairyGUI.Utils;
using DG.Tweening;
using UnityEngine;

namespace FairyGUI
{
	public delegate void PlayCompleteCallback();
	public delegate void TransitionHook();

	public class Transition
	{
		public string name { get; private set; }

		GComponent _owner;
		List<TransitionItem> _items;
		int _totalTimes;
		int _totalTasks;
		bool _playing;
		float _ownerBaseX;
		float _ownerBaseY;
		PlayCompleteCallback _onComplete;
		int _options;

		const int FRAME_RATE = 24;
		static char[] jointChar0 = new char[] { ',' };
		static char[] jointChar1 = new char[] { '|' };
		static char[] jointChar2 = new char[] { '=' };

		const int OPTION_IGNORE_DISPLAY_CONTROLLER = 1;

		public Transition(GComponent owner)
		{
			_owner = owner;
			_items = new List<TransitionItem>();
		}

		public void Play()
		{
			Play(1, 0, null);
		}

		public void Play(PlayCompleteCallback onComplete)
		{
			Play(1, 0, onComplete);
		}

		public void Play(int times, float delay, PlayCompleteCallback onComplete)
		{
			Stop(true, true);

			if (times <= 0)
				times = 1;
			_totalTimes = times;
			InternalPlay(delay);
			_playing = _totalTasks > 0;
			if (_playing)
			{
				_onComplete = onComplete;

				_owner.internalVisible++;
				if ((_options & OPTION_IGNORE_DISPLAY_CONTROLLER) != 0)
				{
					int cnt = _items.Count;
					for (int i = 0; i < cnt; i++)
					{
						TransitionItem item = _items[i];
						if (item.target != null && item.target != _owner)
							item.target.internalVisible++;
					}
				}
			}
			else if (onComplete != null)
				onComplete();
		}

		public void Stop()
		{
			Stop(true, false);
		}

		public void Stop(bool setToComplete, bool processCallback)
		{
			if (_playing)
			{
				_playing = false;
				_totalTasks = 0;
				_totalTimes = 0;
				PlayCompleteCallback func = _onComplete;
				_onComplete = null;

				_owner.internalVisible--;

				int cnt = _items.Count;
				for (int i = 0; i < cnt; i++)
				{
					TransitionItem item = _items[i];
					if (item.target == null)
						continue;

					if ((_options & OPTION_IGNORE_DISPLAY_CONTROLLER) != 0)
					{
						if (item.target != _owner)
							item.target.internalVisible--;
					}

					if (item.completed)
						continue;

					if (item.tweener != null)
					{
						item.tweener.Kill();
						item.tweener = null;
					}

					if (item.type == TransitionActionType.Transition)
					{
						Transition trans = ((GComponent)item.target).GetTransition(item.value.s);
						if (trans != null)
							trans.Stop(setToComplete, false);
					}
					else if (item.type == TransitionActionType.Shake)
					{
						if (Timers.inst.Exists(item.__Shake))
						{
							Timers.inst.Remove(item.__Shake);
							item.target._gearLocked = true;
							item.target.SetXY(item.target.x - item.startValue.f1, item.target.y - item.startValue.f2);
							item.target._gearLocked = false;
						}
					}
					else
					{
						if (setToComplete)
						{
							if (item.tween)
							{
								if (!item.yoyo || item.repeat % 2 == 0)
									ApplyValue(item, item.endValue);
								else
									ApplyValue(item, item.startValue);
							}
							else if (item.type != TransitionActionType.Sound)
								ApplyValue(item, item.value);
						}
					}
				}

				if (processCallback && func != null)
					func();
			}
		}

		public bool playing
		{
			get { return _playing; }
		}

		public void SetValue(string label, params object[] aParams)
		{
			int cnt = _items.Count;
			TransitionValue value;
			for (int i = 0; i < cnt; i++)
			{
				TransitionItem item = _items[i];
				if (item.label == null && item.label2 == null)
					continue;

				if (item.label == label)
				{
					if (item.tween)
						value = item.startValue;
					else
						value = item.value;
				}
				else if (item.label2 == label)
				{
					value = item.endValue;
				}
				else
					continue;

				switch (item.type)
				{
					case TransitionActionType.XY:
					case TransitionActionType.Size:
					case TransitionActionType.Pivot:
					case TransitionActionType.Scale:
						value.b1 = true;
						value.b2 = true;
						value.f1 = Convert.ToSingle(aParams[0]);
						value.f2 = Convert.ToSingle(aParams[1]);
						break;

					case TransitionActionType.Alpha:
						value.f1 = Convert.ToSingle(aParams[0]);
						break;

					case TransitionActionType.Rotation:
						value.i = Convert.ToInt32(aParams[0]);
						break;

					case TransitionActionType.Color:
						value.c = (Color)aParams[0];
						break;

					case TransitionActionType.Animation:
						value.i = Convert.ToInt32(aParams[0]);
						if (aParams.Length > 1)
							value.b = Convert.ToBoolean(aParams[1]);
						break;

					case TransitionActionType.Visible:
						value.b = Convert.ToBoolean(aParams[0]);
						break;

					case TransitionActionType.Controller:
						value.s = (string)aParams[0];
						break;

					case TransitionActionType.Sound:
						value.s = (string)aParams[0];
						if (aParams.Length > 1)
							value.f1 = Convert.ToSingle(aParams[1]);
						break;

					case TransitionActionType.Transition:
						value.s = (string)aParams[0];
						if (aParams.Length > 1)
							value.i = Convert.ToInt32(aParams[1]);
						break;

					case TransitionActionType.Shake:
						value.f1 = Convert.ToSingle(aParams[0]);
						if (aParams.Length > 1)
							value.f2 = Convert.ToSingle(aParams[1]);
						break;
				}
			}
		}

		public void SetHook(string label, TransitionHook callback)
		{
			int cnt = _items.Count;
			for (int i = 0; i < cnt; i++)
			{
				TransitionItem item = _items[i];
				if (item.label == null && item.label2 == null)
					continue;

				if (item.label == label)
				{
					item.hook = callback;
				}
				else if (item.label2 == label)
				{
					item.hook2 = callback;
				}
			}
		}

		public void ClearHooks()
		{
			int cnt = _items.Count;
			for (int i = 0; i < cnt; i++)
			{
				TransitionItem item = _items[i];
				item.hook = null;
				item.hook2 = null;
			}
		}

		public void SetTarget(string label, GObject newTarget)
		{
			int cnt = _items.Count;
			for (int i = 0; i < cnt; i++)
			{
				TransitionItem item = _items[i];
				if (item.label == null && item.label2 == null)
					continue;

				item.targetId = newTarget.id;
			}
		}

		public void Copy(Transition source)
		{
			Stop();
			_items.Clear();
			int cnt = source._items.Count;
			for (int i = 0; i < cnt; i++)
			{
				_items.Add(source._items[i].Clone());
			}
		}

		internal void UpdateFromRelations(string targetId, float dx, float dy)
		{
			int cnt = _items.Count;
			if (cnt == 0)
				return;

			for (int i = 0; i < cnt; i++)
			{
				TransitionItem item = _items[i];
				if (item.type == TransitionActionType.XY && item.targetId == targetId)
				{
					if (item.tween)
					{
						if (item.startValue.b1)
							item.startValue.f1 += dx;
						if (item.startValue.b2)
							item.startValue.f2 += dy;
						if (item.endValue.b1)
							item.endValue.f1 += dx;
						if (item.endValue.b2)
							item.endValue.f2 += dy;
					}
					else
					{
						if (item.value.b1)
							item.value.f1 += dx;
						if (item.value.b2)
							item.value.f2 += dy;
					}
				}
			}
		}

		void InternalPlay(float delay)
		{
			_ownerBaseX = _owner.x;
			_ownerBaseY = _owner.y;

			_totalTasks = 0;
			int cnt = _items.Count;
			for (int i = 0; i < cnt; i++)
			{
				TransitionItem item = _items[i];
				if (item.targetId.Length > 0)
					item.target = _owner.GetChildById(item.targetId);
				else
					item.target = _owner;
				if (item.target == null)
					continue;

				float startTime = delay + item.time;
				if (item.tween)
				{
					item.completed = false;
					switch (item.type)
					{
						case TransitionActionType.XY:
						case TransitionActionType.Size:
							_totalTasks++;
							if (startTime == 0)
								StartTween(item);
							else
							{
								item.value.f3 = 1;
								item.tweener = DOVirtual.DelayedCall(startTime, () =>
								{
									item.tweener = null;
									StartTween(item);
								});
							}
							break;

						case TransitionActionType.Scale:
							_totalTasks++;
							item.value.f1 = item.startValue.f1;
							item.value.f2 = item.startValue.f2;
							item.tweener = DOTween.To(() => new Vector2(item.value.f1, item.value.f2),
								val =>
								{
									item.value.f1 = val.x;
									item.value.f2 = val.y;
								}, new Vector2(item.endValue.f1, item.endValue.f2), item.duration)
								.SetEase(item.easeType)
								.OnStart(() => { if (item.hook != null) item.hook(); })
								.OnUpdate(() => { ApplyValue(item, item.value); })
								.OnComplete(() => { tweenComplete(item); });
							if (startTime > 0)
								item.tweener.SetDelay(startTime);
							else
								ApplyValue(item, item.value);
							if (item.repeat > 0)
								item.tweener.SetLoops(item.repeat + 1, item.yoyo ? LoopType.Yoyo : LoopType.Restart);
							break;

						case TransitionActionType.Alpha:
							_totalTasks++;
							item.value.f1 = item.startValue.f1;
							item.tweener = DOTween.To(() => item.value.f1, v => item.value.f1 = v, item.endValue.f1, item.duration)
								.SetEase(item.easeType)
								.OnStart(() => { if (item.hook != null) item.hook(); })
								.OnUpdate(() => { ApplyValue(item, item.value); })
								.OnComplete(() => { tweenComplete(item); });
							if (startTime > 0)
								item.tweener.SetDelay(startTime);
							else
								ApplyValue(item, item.value);
							if (item.repeat > 0)
								item.tweener.SetLoops(item.repeat + 1, item.yoyo ? LoopType.Yoyo : LoopType.Restart);
							break;

						case TransitionActionType.Rotation:
							_totalTasks++;
							item.value.i = item.startValue.i;
							item.tweener = DOTween.To(() => item.value.i, v => item.value.i = v, item.endValue.i, item.duration)
								.SetEase(item.easeType)
								.OnStart(() => { if (item.hook != null) item.hook(); })
								.OnUpdate(() => { ApplyValue(item, item.value); })
								.OnComplete(() => { tweenComplete(item); });
							if (startTime > 0)
								item.tweener.SetDelay(startTime);
							else
								ApplyValue(item, item.value);
							if (item.repeat > 0)
								item.tweener.SetLoops(item.repeat + 1, item.yoyo ? LoopType.Yoyo : LoopType.Restart);
							break;
					}
				}
				else
				{
					if (startTime == 0)
						ApplyValue(item, item.value);
					else
					{
						item.completed = false;
						_totalTasks++;
						item.value.f3 = 1;
						item.tweener = DOVirtual.DelayedCall(startTime, () =>
						{
							item.tweener = null;
							item.completed = true;
							_totalTasks--;

							ApplyValue(item, item.value);
							if (item.hook != null)
								item.hook();

							CheckAllComplete();
						}, DOTween.defaultTimeScaleIndependent);
					}
				}
			}
		}

		void StartTween(TransitionItem item)
		{
			if (item.type == TransitionActionType.XY)
			{
				if (item.target == _owner)
				{
					item.value.f1 = item.startValue.b1 ? item.startValue.f1 : 0;
					item.value.f2 = item.startValue.b2 ? item.startValue.f2 : 0;
				}
				else
				{
					item.value.f1 = item.startValue.b1 ? item.startValue.f1 : item.target.x;
					item.value.f2 = item.startValue.b2 ? item.startValue.f2 : item.target.y;
				}
			}
			else
			{
				item.value.f1 = item.startValue.b1 ? item.startValue.f1 : item.target.width;
				item.value.f2 = item.startValue.b2 ? item.startValue.f2 : item.target.height;
			}

			Vector2 endValue = new Vector2(item.endValue.b1 ? item.endValue.f1 : item.value.f1, item.endValue.b2 ? item.endValue.f2 : item.value.f2);
			item.tweener = DOTween.To(() => new Vector2(item.value.f1, item.value.f2),
						val =>
						{
							item.value.f1 = val.x;
							item.value.f2 = val.y;
						}, endValue, item.duration)
					.SetEase(item.easeType)
					.OnUpdate(() => { ApplyValue(item, item.value); })
					.OnComplete(() => { tweenComplete(item); });
			if (item.repeat > 0)
				item.tweener.SetLoops(item.repeat + 1, item.yoyo ? LoopType.Yoyo : LoopType.Restart);

			ApplyValue(item, item.value);

			if (item.hook != null)
				item.hook();
		}

		void tweenComplete(TransitionItem item)
		{
			item.tweener = null;
			item.completed = true;
			_totalTasks--;
			if (item.hook2 != null)
				item.hook2();

			if (item.type == TransitionActionType.XY || item.type == TransitionActionType.Size
				|| item.type == TransitionActionType.Scale)
				_owner.InvalidateBatchingState();

			CheckAllComplete();
		}

		void __playTransComplete(TransitionItem item)
		{
			_totalTasks--;
			item.completed = true;
			CheckAllComplete();
		}

		void CheckAllComplete()
		{
			if (_playing && _totalTasks == 0)
			{
				if (_totalTimes < 0)
				{
					InternalPlay(0);
				}
				else
				{
					_totalTimes--;
					if (_totalTimes > 0)
						InternalPlay(0);
					else
					{
						_playing = false;
						_owner.internalVisible--;

						if ((_options & OPTION_IGNORE_DISPLAY_CONTROLLER) != 0)
						{
							int cnt = _items.Count;
							for (int i = 0; i < cnt; i++)
							{
								TransitionItem item = _items[i];
								if (item.target != null && item.target != _owner)
									item.target.internalVisible--;
							}
						}

						if (_onComplete != null)
						{
							PlayCompleteCallback func = _onComplete;
							_onComplete = null;
							func();
						}
					}
				}
			}
		}

		void ApplyValue(TransitionItem item, TransitionValue value)
		{
			item.target._gearLocked = true;

			switch (item.type)
			{
				case TransitionActionType.XY:
					if (item.target == _owner)
					{
						float f1, f2;
						if (!value.b1)
							f1 = item.target.x;
						else
							f1 = value.f1 + _ownerBaseX;
						if (!value.b2)
							f2 = item.target.y;
						else
							f2 = value.f2 + _ownerBaseY;
						item.target.SetXY(f1, f2);
					}
					else
					{
						if (!value.b1)
							value.f1 = item.target.x;
						if (!value.b2)
							value.f2 = item.target.y;
						item.target.SetXY(value.f1, value.f2);
					}
					break;

				case TransitionActionType.Size:
					if (!value.b1)
						value.f1 = item.target.width;
					if (!value.b2)
						value.f2 = item.target.height;
					item.target.SetSize(value.f1, value.f2);
					break;

				case TransitionActionType.Pivot:
					item.target.SetPivot(value.f1, value.f2);
					break;

				case TransitionActionType.Alpha:
					item.target.alpha = value.f1;
					break;

				case TransitionActionType.Rotation:
					item.target.rotation = value.i;
					break;

				case TransitionActionType.Scale:
					item.target.SetScale(value.f1, value.f2);
					break;

				case TransitionActionType.Color:
					((IColorGear)item.target).color = value.c;
					break;

				case TransitionActionType.Animation:
					if (!value.b1)
						value.i = ((IAnimationGear)item.target).frame;
					((IAnimationGear)item.target).frame = value.i;
					((IAnimationGear)item.target).playing = value.b;
					break;

				case TransitionActionType.Visible:
					item.target.visible = value.b;
					break;

				case TransitionActionType.Controller:
					string[] arr = value.s.Split(jointChar0);
					foreach (string str in arr)
					{
						string[] arr2 = str.Split(jointChar2);
						Controller cc = ((GComponent)item.target).GetController(arr2[0]);
						if (cc != null)
						{
							string str2 = arr2[1];
							if (str2[0] == '$')
							{
								str2 = str.Substring(1);
								cc.selectedPage = str2;
							}
							else
								cc.selectedIndex = int.Parse(str2);
						}
					}
					break;

				case TransitionActionType.Transition:
					Transition trans = ((GComponent)item.target).GetTransition(value.s);
					if (trans != null)
					{
						if (value.i == 0)
							trans.Stop(false, true);
						else if (trans.playing)
							trans._totalTimes = value.i;
						else
						{
							item.completed = false;
							_totalTasks++;
							trans.Play(value.i, 0, () => { __playTransComplete(item); });
						}
					}
					break;

				case TransitionActionType.Sound:
					AudioClip sound = UIPackage.GetItemAssetByURL(value.s) as AudioClip;
					if (sound != null)
						Stage.inst.PlayOneShotSound(sound, value.f1);
					break;

				case TransitionActionType.Shake:
					item.startValue.f1 = 0; //offsetX
					item.startValue.f2 = 0; //offsetY
					item.startValue.f3 = item.value.f2;//shakePeriod
					Timers.inst.Add(0.001f, 0, item.__Shake, this);
					_totalTasks++;
					item.completed = false;
					break;
			}

			item.target._gearLocked = false;
		}

		internal void ShakeItem(TransitionItem item)
		{
			float r = Mathf.Ceil(item.value.f1 * item.startValue.f3 / item.value.f2);
			Vector2 vr = UnityEngine.Random.insideUnitCircle * r;
			vr.x = vr.x > 0 ? Mathf.Ceil(vr.x) : Mathf.Floor(vr.x);
			vr.y = vr.y > 0 ? Mathf.Ceil(vr.y) : Mathf.Floor(vr.y);

			item.target._gearLocked = true;
			item.target.SetXY(item.target.x - item.startValue.f1 + vr.x, item.target.y - item.startValue.f2 + vr.y);
			item.target._gearLocked = false;

			item.startValue.f1 = vr.x;
			item.startValue.f2 = vr.y;
			item.startValue.f3 -= Time.deltaTime;
			if (item.startValue.f3 <= 0)
			{
				item.target._gearLocked = true;
				item.target.SetXY(item.target.x - item.startValue.f1, item.target.y - item.startValue.f2);
				item.target._gearLocked = false;

				item.completed = true;
				_totalTasks--;
				Timers.inst.Remove(item.__Shake);

				CheckAllComplete();
			}
		}

		public void Setup(XML xml)
		{
			this.name = xml.GetAttribute("name");
			_options = xml.GetAttributeInt("options");
			XMLList col = xml.Elements("item");

			foreach (XML cxml in col)
			{
				TransitionItem item = new TransitionItem();
				_items.Add(item);

				item.time = (float)cxml.GetAttributeInt("time") / (float)FRAME_RATE;
				item.targetId = cxml.GetAttribute("target", string.Empty);
				item.type = FieldTypes.ParseTransitionActionType(cxml.GetAttribute("type"));
				item.tween = cxml.GetAttributeBool("tween");
				item.label = cxml.GetAttribute("label");
				if (item.tween)
				{
					item.duration = (float)cxml.GetAttributeInt("duration") / FRAME_RATE;

					string ease = cxml.GetAttribute("ease");
					if (ease != null)
						item.easeType = FieldTypes.ParseEaseType(ease);

					item.repeat = cxml.GetAttributeInt("repeat");
					item.yoyo = cxml.GetAttributeBool("yoyo");
					item.label2 = cxml.GetAttribute("label2");

					string v = cxml.GetAttribute("endValue");
					if (v != null)
					{
						DecodeValue(item.type, cxml.GetAttribute("startValue", string.Empty), item.startValue);
						DecodeValue(item.type, v, item.endValue);
					}
					else
					{
						item.tween = false;
						DecodeValue(item.type, cxml.GetAttribute("startValue", string.Empty), item.value);
					}
				}
				else
				{
					DecodeValue(item.type, cxml.GetAttribute("value", string.Empty), item.value);
				}
			}
		}

		void DecodeValue(TransitionActionType type, string str, TransitionValue value)
		{
			string[] arr;
			switch (type)
			{
				case TransitionActionType.XY:
				case TransitionActionType.Size:
				case TransitionActionType.Pivot:
					arr = str.Split(jointChar0);
					if (arr[0] == "-")
					{
						value.b1 = false;
					}
					else
					{
						value.f1 = int.Parse(arr[0]);
						value.b1 = true;
					}
					if (arr[1] == "-")
					{
						value.b2 = false;
					}
					else
					{
						value.f2 = int.Parse(arr[1]);
						value.b2 = true;
					}
					break;

				case TransitionActionType.Alpha:
					value.f1 = float.Parse(str);
					break;

				case TransitionActionType.Rotation:
					value.i = int.Parse(str);
					break;

				case TransitionActionType.Scale:
					arr = str.Split(jointChar0);
					value.f1 = float.Parse(arr[0]);
					value.f2 = float.Parse(arr[1]);
					break;

				case TransitionActionType.Color:
					value.c = ToolSet.ConvertFromHtmlColor(str);
					break;

				case TransitionActionType.Animation:
					arr = str.Split(jointChar0);
					if (arr[0] == "-")
					{
						value.b1 = false;
					}
					else
					{
						value.i = int.Parse(arr[0]);
						value.b1 = true;
					}
					value.b = arr[1] == "p";
					break;

				case TransitionActionType.Visible:
					value.b = str == "true";
					break;

				case TransitionActionType.Controller:
					value.s = str;
					break;

				case TransitionActionType.Sound:
					arr = str.Split(jointChar0);
					value.s = arr[0];
					if (arr.Length > 1)
					{
						int intv = int.Parse(arr[1]);
						if (intv == 100 || intv == 0)
							value.f1 = 1;
						else
							value.f1 = (float)intv / 100f;
					}
					else
						value.f1 = 1;
					break;

				case TransitionActionType.Transition:
					arr = str.Split(jointChar0);
					value.s = arr[0];
					if (arr.Length > 1)
						value.i = int.Parse(arr[1]);
					else
						value.i = 1;
					break;

				case TransitionActionType.Shake:
					arr = str.Split(jointChar0);
					value.f1 = float.Parse(arr[0]);
					value.f2 = float.Parse(arr[1]);
					break;
			}
		}
	}

	public class TransitionItem
	{
		public float time;
		public string targetId;
		public TransitionActionType type;
		public float duration;
		public TransitionValue value;
		public TransitionValue startValue;
		public TransitionValue endValue;
		public Ease easeType;
		public int repeat;
		public bool yoyo;
		public bool tween;
		public string label;
		public string label2;

		//hooks
		public TransitionHook hook;
		public TransitionHook hook2;

		//running properties
		public Tween tweener;
		public bool completed;
		public GObject target;

		public TransitionItem()
		{
			easeType = Ease.OutQuad;
			value = new TransitionValue();
			startValue = new TransitionValue();
			endValue = new TransitionValue();
		}

		public TransitionItem Clone()
		{
			TransitionItem item = new TransitionItem();
			item.time = this.time;
			item.targetId = this.targetId;
			item.type = this.type;
			item.duration = this.duration;
			item.value.Copy(this.value);
			item.startValue.Copy(this.startValue);
			item.endValue.Copy(this.endValue);
			item.easeType = this.easeType;
			item.repeat = this.repeat;
			item.yoyo = this.yoyo;
			item.tween = this.tween;
			item.label = this.label;
			item.label2 = this.label2;
			return item;
		}

		public void __Shake(object callback)
		{
			((Transition)callback).ShakeItem(this);
		}
	}

	public class TransitionValue
	{
		public float f1;//x, scalex, pivotx,alpha,shakeAmplitude
		public float f2;//y, scaley, pivoty, shakePeriod
		public float f3;
		public int i;//rotation,frame
		public Color c;//color
		public bool b;//playing
		public string s;//sound,transName

		public bool b1;
		public bool b2;

		public TransitionValue()
		{
			b1 = true;
			b2 = true;
		}

		public void Copy(TransitionValue source)
		{
			this.f1 = source.f1;
			this.f2 = source.f2;
			this.f3 = source.f3;
			this.i = source.i;
			this.c = source.c;
			this.b = source.b;
			this.s = source.s;
			this.b1 = source.b1;
			this.b2 = source.b2;
		}
	}
}
