import { useCallback, useEffect, useRef, useState } from 'react';

const MUTE_KEY = 'school-admission.voiceMuted';
const SPOKEN_KEY = 'school-admission.voiceSpoken';

const VOICE_LANG = 'bn-BD';

export const WELCOME_MESSAGE =
  'স্কুল অ্যাডমিশন সিস্টেমে আপনাকে স্বাগতম। ' +
  'শুরু করতে, পেজের উপরে আপনার সতেরো সংখ্যার রেজিস্ট্রেশন নম্বর লিখুন। ' +
  'ক্লাসের তালিকা দেখে কোন ক্লাসে কতটি আসন খালি আছে তা জেনে নিন, ' +
  'তারপর অ্যাপ্লাই বাটনে ক্লিক করে আপনার নামে একটি আসন হোল্ড করুন। ' +
  'হোল্ড করা আসন নির্দিষ্ট সময়ের জন্য সংরক্ষিত থাকে, যা কাউন্টডাউনে দেখানো হয়। ' +
  'সময় শেষ হওয়ার আগে ভর্তি ফি পরিশোধ করতে পে অ্যাডমিশন ফি বাটনে ক্লিক করুন ' +
  'অথবা আসন ছেড়ে দিতে ক্যান্সেল বাটনে ক্লিক করুন। ' +
  'আপনার সব আবেদন এবং তাদের অবস্থা মাই অ্যাপ্লিকেশনস অংশে দেখতে পাবেন। শুভকামনা!';

function speechSupported(): boolean {
  return typeof window !== 'undefined' && 'speechSynthesis' in window;
}

/** Picks an installed Bengali voice, falling back to null if none is available. */
function pickBengaliVoice(): SpeechSynthesisVoice | null {
  if (!speechSupported()) return null;
  const voices = window.speechSynthesis.getVoices();
  return (
    voices.find((v) => v.lang?.toLowerCase().startsWith('bn')) ??
    voices.find((v) => v.lang?.toLowerCase().replace('_', '-') === VOICE_LANG) ??
    null
  );
}

/**
 * Speaks a short welcome + how-to guide using the browser's built-in
 * SpeechSynthesis API. It tries to speak on load; since browsers block audio
 * before any user gesture, it also retries on the first interaction. A mute
 * preference is remembered across visits.
 */
export function useVoiceGuide() {
  const supported = speechSupported();
  const [muted, setMuted] = useState<boolean>(() => {
    try {
      return localStorage.getItem(MUTE_KEY) === 'true';
    } catch {
      return false;
    }
  });
  const [speaking, setSpeaking] = useState(false);
  const hasSpokenRef = useRef(false);

  const stop = useCallback(() => {
    if (!supported) return;
    window.speechSynthesis.cancel();
    setSpeaking(false);
  }, [supported]);

  const speak = useCallback(
    (text: string) => {
      if (!supported) return;
      window.speechSynthesis.cancel();
      const utterance = new SpeechSynthesisUtterance(text);
      utterance.lang = VOICE_LANG;
      const bnVoice = pickBengaliVoice();
      if (bnVoice) utterance.voice = bnVoice;
      utterance.rate = 1;
      utterance.pitch = 1;
      utterance.onend = () => setSpeaking(false);
      utterance.onerror = () => setSpeaking(false);
      setSpeaking(true);
      window.speechSynthesis.speak(utterance);
    },
    [supported],
  );

  const replay = useCallback(() => {
    hasSpokenRef.current = true;
    try {
      sessionStorage.setItem(SPOKEN_KEY, 'true');
    } catch {
      /* ignore */
    }
    speak(WELCOME_MESSAGE);
  }, [speak]);

  const toggleMute = useCallback(() => {
    setMuted((prev) => {
      const next = !prev;
      try {
        localStorage.setItem(MUTE_KEY, String(next));
      } catch {
        /* ignore */
      }
      if (next) {
        window.speechSynthesis?.cancel();
        setSpeaking(false);
      }
      return next;
    });
  }, []);

  // Greet once per browser tab session, on load and (as a fallback) on the
  // first user interaction if the browser blocked the on-load attempt.
  useEffect(() => {
    if (!supported || muted) return;

    let alreadySpoken = false;
    try {
      alreadySpoken = sessionStorage.getItem(SPOKEN_KEY) === 'true';
    } catch {
      /* ignore */
    }
    if (alreadySpoken) {
      hasSpokenRef.current = true;
      return;
    }

    const greet = () => {
      if (hasSpokenRef.current) return;
      hasSpokenRef.current = true;
      try {
        sessionStorage.setItem(SPOKEN_KEY, 'true');
      } catch {
        /* ignore */
      }
      speak(WELCOME_MESSAGE);
      removeListeners();
    };

    const removeListeners = () => {
      window.removeEventListener('pointerdown', greet);
      window.removeEventListener('keydown', greet);
    };

    // Attempt immediately on load.
    greet();

    // Safety net: if the browser blocked the on-load speech, speak on first input.
    if (!window.speechSynthesis.speaking) {
      hasSpokenRef.current = false;
      try {
        sessionStorage.removeItem(SPOKEN_KEY);
      } catch {
        /* ignore */
      }
      window.addEventListener('pointerdown', greet, { once: false });
      window.addEventListener('keydown', greet, { once: false });
    }

    return removeListeners;
    // Run once on mount.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [supported]);

  return { supported, muted, speaking, toggleMute, replay, stop };
}
