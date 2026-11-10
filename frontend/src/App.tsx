import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Container, Navbar } from 'react-bootstrap';
import { api, ApiRequestError } from './api';
import { ClassList } from './components/ClassList';
import { CreateClass } from './components/CreateClass';
import { MyApplications } from './components/MyApplications';
import { StudentIdBar } from './components/StudentIdBar';
import type { ClassSummary, MyHold } from './types';
import { useStudentId } from './useStudentId';

const POLL_INTERVAL_MS = 3000;

export default function App() {
  const { studentId, setStudentId } = useStudentId();
  const [classes, setClasses] = useState<ClassSummary[]>([]);
  const [myHolds, setMyHolds] = useState<MyHold[]>([]);
  const [classesLoading, setClassesLoading] = useState(true);
  const [holdsLoading, setHoldsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Keep the latest studentId in a ref so the polling effect doesn't restart on change.
  const studentIdRef = useRef(studentId);
  studentIdRef.current = studentId;

  const refreshClasses = useCallback(async () => {
    try {
      const data = await api.listClasses();
      setClasses(data);
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : 'Failed to load classes.');
    } finally {
      setClassesLoading(false);
    }
  }, []);

  const refreshMyHolds = useCallback(async () => {
    const sid = studentIdRef.current;
    if (sid.length !== 17) {
      setMyHolds([]);
      return;
    }
    setHoldsLoading(true);
    try {
      const data = await api.myHolds(sid);
      setMyHolds(data);
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : 'Failed to load your applications.');
    } finally {
      setHoldsLoading(false);
    }
  }, []);

  // Poll both lists so seat counts and application statuses stay fresh without a reload.
  useEffect(() => {
    refreshClasses();
    refreshMyHolds();
    const id = setInterval(() => {
      refreshClasses();
      refreshMyHolds();
    }, POLL_INTERVAL_MS);
    return () => clearInterval(id);
  }, [refreshClasses, refreshMyHolds]);

  // Refresh "my applications" immediately when a valid id is entered.
  useEffect(() => {
    if (studentId.length === 17) {
      refreshMyHolds();
    } else {
      setMyHolds([]);
    }
  }, [studentId, refreshMyHolds]);

  const handleApplied = useCallback(() => {
    refreshClasses();
    refreshMyHolds();
  }, [refreshClasses, refreshMyHolds]);

  return (
    <>
      <Navbar bg="dark" variant="dark" className="mb-4">
        <Container>
          <Navbar.Brand>🎓 School Admission System</Navbar.Brand>
        </Container>
      </Navbar>

      <Container className="pb-5">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)} dismissible>
            {error}
          </Alert>
        )}

        <StudentIdBar studentId={studentId} onChange={setStudentId} />

        <CreateClass onCreated={refreshClasses} />

        <ClassList
          classes={classes}
          loading={classesLoading}
          studentId={studentId}
          onApplied={handleApplied}
          onError={setError}
        />

        <MyApplications
          holds={myHolds}
          loading={holdsLoading}
          studentId={studentId}
          onChanged={handleApplied}
          onError={setError}
        />
      </Container>
    </>
  );
}
