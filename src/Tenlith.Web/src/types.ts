export type Role = "Owner" | "Admin" | "Member";
export type Organization = {
  id: string;
  name: string;
  slug: string;
  role: Role;
  plan: "Free" | "Pro";
  createdAt: string;
};
export type Project = {
  id: string;
  name: string;
  description: string;
  color: string;
  status: "Active" | "Archived";
  createdAt: string;
};
export type Member = {
  id: string;
  userId: string;
  name: string;
  email: string;
  role: Role;
  joinedAt: string;
};
export type Invitation = {
  id: string;
  email: string;
  role: Role;
  expiresAt: string;
};
export type Billing = {
  plan: "Free" | "Pro";
  status: string;
  provider: "simulation" | "stripe" | "unconfigured";
  projectLimit: number;
  memberLimit: number;
  canManage: boolean;
  configured: boolean;
};
export type Overview = {
  projectCount: number;
  memberCount: number;
  plan: string;
  projectLimit: number;
  memberLimit: number;
  activity: {
    id: string;
    action: string;
    actorName: string;
    createdAt: string;
  }[];
};
