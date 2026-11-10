import { useCallback, useEffect, useState } from 'react';

const STORAGE_KEY = 'school-admission.studentId';

/**
 * Stores the student's 17-digit registration number locally so the identity
 * survives page refreshes and browser restarts (no login required).
 */
export function useStudentId() {
  const [studentId, setStudentIdState] = useState<string>(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) ?? '';
    } catch {
      return '';
    }
  });

  useEffect(() => {
    try {
      if (studentId) {
        localStorage.setItem(STORAGE_KEY, studentId);
      } else {
        localStorage.removeItem(STORAGE_KEY);
      }
    } catch {
      /* ignore storage errors */
    }
  }, [studentId]);

  const setStudentId = useCallback((value: string) => {
    // Keep only digits; the backend expects a numeric registration number.
    setStudentIdState(value.replace(/\D/g, '').slice(0, 17));
  }, []);

  return { studentId, setStudentId };
}
