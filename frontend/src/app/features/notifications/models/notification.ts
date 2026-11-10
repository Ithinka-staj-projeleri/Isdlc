/** One notification of the signed-in user (GET /api/v1/notifications). readAt is null while unread. */
export interface AppNotification {
    id: number;
    project: { id: number; code: string; name: string };
    message: string;
    createdAt: string;
    readAt: string | null;
}

export interface UnreadCount {
    count: number;
}
