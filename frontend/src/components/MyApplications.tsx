import { useState } from 'react';
import { Badge, Button, Card, Spinner, Table } from 'react-bootstrap';
import { api, ApiRequestError } from '../api';
import type { MyHold } from '../types';
import { formatRemaining, useNow } from '../useNow';

interface Props {
  holds: MyHold[];
  loading: boolean;
  studentId: string;
  onChanged: () => void;
  onError: (message: string) => void;
}

function statusBadge(status: MyHold['status']) {
  switch (status) {
    case 'HELD':
      return <Badge bg="warning" text="dark">HELD</Badge>;
    case 'CONFIRMED':
      return <Badge bg="success">CONFIRMED</Badge>;
    case 'EXPIRED':
      return <Badge bg="secondary">EXPIRED</Badge>;
    case 'CANCELLED':
      return <Badge bg="dark">CANCELLED</Badge>;
    default:
      return <Badge bg="light" text="dark">{status}</Badge>;
  }
}

export function MyApplications({ holds, loading, studentId, onChanged, onError }: Props) {
  const now = useNow(1000);
  const [busyId, setBusyId] = useState<string | null>(null);

  async function handleConfirm(holdId: string) {
    setBusyId(holdId);
    try {
      await api.confirmHold(holdId, studentId);
      onChanged();
    } catch (err) {
      onError(err instanceof ApiRequestError ? err.message : 'Failed to pay the fee.');
      onChanged();
    } finally {
      setBusyId(null);
    }
  }

  async function handleCancel(holdId: string) {
    setBusyId(holdId);
    try {
      await api.cancelHold(holdId, studentId);
      onChanged();
    } catch (err) {
      onError(err instanceof ApiRequestError ? err.message : 'Failed to cancel.');
      onChanged();
    } finally {
      setBusyId(null);
    }
  }

  if (!studentId) {
    return null;
  }

  return (
    <Card className="mb-4">
      <Card.Header className="d-flex justify-content-between align-items-center">
        <strong>My Applications</strong>
        {loading && <Spinner animation="border" size="sm" />}
      </Card.Header>
      <Card.Body className="p-0">
        {holds.length === 0 ? (
          <p className="text-muted p-3 mb-0">You have no applications yet. Apply to a class above.</p>
        ) : (
          <Table responsive hover className="mb-0 align-middle">
            <thead>
              <tr>
                <th>Class</th>
                <th>Status</th>
                <th>Time left</th>
                <th className="text-end">Actions</th>
              </tr>
            </thead>
            <tbody>
              {holds.map((h) => {
                const expiresAtMs = new Date(h.expiresAt).getTime();
                const remaining = expiresAtMs - now;
                // Treat an elapsed countdown as expired even before the next poll,
                // so the UI updates without a page reload.
                const effectiveStatus =
                  h.status === 'HELD' && remaining <= 0 ? 'EXPIRED' : h.status;
                const canAct = effectiveStatus === 'HELD';
                return (
                  <tr key={h.holdId}>
                    <td>{h.className}</td>
                    <td>{statusBadge(effectiveStatus)}</td>
                    <td>
                      {effectiveStatus === 'HELD'
                        ? formatRemaining(remaining)
                        : <span className="text-muted">—</span>}
                    </td>
                    <td className="text-end">
                      <Button
                        size="sm"
                        variant="success"
                        className="me-2"
                        disabled={!canAct || busyId === h.holdId}
                        onClick={() => handleConfirm(h.holdId)}
                      >
                        {busyId === h.holdId ? '…' : 'Pay admission fee'}
                      </Button>
                      <Button
                        size="sm"
                        variant="outline-danger"
                        disabled={!canAct || busyId === h.holdId}
                        onClick={() => handleCancel(h.holdId)}
                      >
                        Cancel
                      </Button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </Table>
        )}
      </Card.Body>
    </Card>
  );
}
