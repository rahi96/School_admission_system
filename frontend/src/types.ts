export type HoldStatus = 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED';

export interface ClassSummary {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
}

export interface CreatedClass {
  id: string;
  name: string;
  totalSeats: number;
}

export interface Hold {
  holdId: string;
  classId: string;
  studentId: string;
  status: HoldStatus;
  expiresAt: string;
}

export interface MyHold {
  holdId: string;
  classId: string;
  className: string;
  status: HoldStatus;
  expiresAt: string;
}

export interface ApiError {
  error: string;
  message: string;
}
