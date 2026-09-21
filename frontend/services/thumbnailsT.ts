export type ThumbnailEntry = {
    targetId: number;
    imageUrl: string|null;
    state: ThumbnailState;
}

export enum ThumbnailState {
    Error = 1,
    Completed,
    InReview,
    Pending,
    Blocked,
}
