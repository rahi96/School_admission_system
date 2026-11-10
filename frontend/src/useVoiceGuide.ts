import { useCallback, useEffect, useRef, useState } from 'react';

const MUTE_KEY = 'school-admission.voiceMuted';
const SPOKEN_KEY = 'school-admission.voiceSpoken';

const VOICE_LANG = 'en-US';

export const WELCOME_MESSAGE =
  'Welcome to the School Admission System. ' +
  'To get started, enter your seventeen digit registration number at the top of the page. ' +
  'Browse the list of classes to see how many seats are available, ' +
  'then click Apply to hold a seat in your name. ' +
  'A held seat is reserved for a limited time, shown by a countdown. ' +
  'Click Pay admission fee before the timer runs out to confirm your admission, ' +
  'or click Cancel to release the seat. ' +
  'You can see all of your applications and their status under My Applications. Good luck!';

function speechSupported(): boolean {
  return typeof window !== 'undefined' && 'speechSynthesis' in window;
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
