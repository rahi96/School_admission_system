import { Card, Col, Form, Row } from 'react-bootstrap';

interface Props {
  studentId: string;
  onChange: (value: string) => void;
}

export function StudentIdBar({ studentId, onChange }: Props) {
  const valid = studentId.length === 17;
  return (
    <Card className="mb-4">
      <Card.Body>
        <Row className="align-items-center g-2">
          <Col xs={12} md="auto">
            <Form.Label className="mb-0 fw-bold">Your registration number</Form.Label>
          </Col>
          <Col xs={12} md={5}>
            <Form.Control
              type="text"
              inputMode="numeric"
              placeholder="17-digit registration number"
              value={studentId}
              onChange={(e) => onChange(e.target.value)}
              isValid={valid}
              isInvalid={studentId.length > 0 && !valid}
            />
          </Col>
          <Col xs={12} md="auto">
            {studentId.length === 0 ? (
              <span className="text-muted">Enter your number to apply and view your applications.</span>
            ) : valid ? (
              <span className="text-success">Looks good — this identifies you across the system.</span>
            ) : (
              <span className="text-danger">{studentId.length}/17 digits</span>
            )}
          </Col>
        </Row>
      </Card.Body>
    </Card>
  );
}
