import { useState } from 'react';
import { Badge, Button, Card, Spinner, Table } from 'react-bootstrap';
import { api, ApiRequestError } from '../api';
import type { ClassSummary } from '../types';

interface Props {
  classes: ClassSummary[];
  loading: boolean;
  studentId: string;
  onApplied: () => void;
  onError: (message: string) => void;
}

export function ClassList({ classes, loading, studentId, onApplied, onError }: Props) {
  const [applyingId, setApplyingId] = useState<string | null>(null);

  async function handleApply(classId: string) {
    if (!studentId) {
      onError('Please enter your registration number first.');
      return;
    }
    setApplyingId(classId);
    try {
      await api.createHold(classId, studentId);
      onApplied();
    } catch (err) {
      onError(err instanceof ApiRequestError ? err.message : 'Failed to apply.');
    } finally {
      setApplyingId(null);
    }
  }

  return (
    <Card className="mb-4">
      <Card.Header className="d-flex justify-content-between align-items-center">
        <strong>Classes</strong>
        {loading && <Spinner animation="border" size="sm" />}
      </Card.Header>
      <Card.Body className="p-0">
        {classes.length === 0 && !loading ? (
          <p className="text-muted p-3 mb-0">No classes yet. Create one above to get started.</p>
        ) : (
          <Table responsive hover className="mb-0 align-middle">
            <thead>
              <tr>
                <th>Class</th>
                <th className="text-center">Total</th>
                <th className="text-center">Held</th>
                <th className="text-center">Confirmed</th>
                <th className="text-center">Available</th>
                <th className="text-end">Action</th>
              </tr>
            </thead>
            <tbody>
              {classes.map((c) => {
                const soldOut = c.availableSeats <= 0;
                return (
                  <tr key={c.id}>
                    <td>{c.name}</td>
                    <td className="text-center">{c.totalSeats}</td>
                    <td className="text-center">
                      <Badge bg="warning" text="dark">{c.heldSeats}</Badge>
                    </td>
                    <td className="text-center">
                      <Badge bg="success">{c.confirmedSeats}</Badge>
                    </td>
                    <td className="text-center">
                      <Badge bg={soldOut ? 'secondary' : 'primary'}>{c.availableSeats}</Badge>
                    </td>
                    <td className="text-end">
                      <Button
                        size="sm"
                        variant={soldOut ? 'outline-secondary' : 'outline-primary'}
                        disabled={soldOut || applyingId === c.id}
                        onClick={() => handleApply(c.id)}
                      >
                        {applyingId === c.id ? 'Applying…' : soldOut ? 'Full' : 'Apply'}
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
