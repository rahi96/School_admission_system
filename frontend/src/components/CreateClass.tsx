import { useState } from 'react';
import { Alert, Button, Card, Col, Form, Row } from 'react-bootstrap';
import { api, ApiRequestError } from '../api';

interface Props {
  onCreated: () => void;
}

export function CreateClass({ onCreated }: Props) {
  const [name, setName] = useState('');
  const [totalSeats, setTotalSeats] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    const trimmedName = name.trim();
    const seats = Number(totalSeats);

    if (!trimmedName) {
      setError('Please enter a class name.');
      return;
    }
    if (!Number.isInteger(seats) || seats <= 0) {
      setError('Total seats must be a positive whole number.');
      return;
    }

    setSubmitting(true);
    try {
      const created = await api.createClass(trimmedName, seats);
      setSuccess(`Created "${created.name}" with ${created.totalSeats} seats.`);
      setName('');
      setTotalSeats('');
      onCreated();
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : 'Failed to create class.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Card className="mb-4">
      <Card.Header>
        <strong>Create a Class</strong> <span className="text-muted">(school admin)</span>
      </Card.Header>
      <Card.Body>
        {error && <Alert variant="danger" onClose={() => setError(null)} dismissible>{error}</Alert>}
        {success && <Alert variant="success" onClose={() => setSuccess(null)} dismissible>{success}</Alert>}
        <Form onSubmit={handleSubmit}>
          <Row className="g-2 align-items-end">
            <Col xs={12} md={6}>
              <Form.Label>Class name</Form.Label>
              <Form.Control
                type="text"
                placeholder="e.g. Class 6 - Morning Shift"
                value={name}
                onChange={(e) => setName(e.target.value)}
                disabled={submitting}
              />
            </Col>
            <Col xs={8} md={3}>
              <Form.Label>Total seats</Form.Label>
              <Form.Control
                type="number"
                min={1}
                placeholder="120"
                value={totalSeats}
                onChange={(e) => setTotalSeats(e.target.value)}
                disabled={submitting}
              />
            </Col>
            <Col xs={4} md={3}>
              <Button type="submit" variant="primary" className="w-100" disabled={submitting}>
                {submitting ? 'Creating…' : 'Create'}
              </Button>
            </Col>
          </Row>
        </Form>
      </Card.Body>
    </Card>
  );
}
