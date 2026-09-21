export class List<T> {
    private list: T[] = [];

    constructor(listData?: T[]) {
        if (listData)
            this.list = listData;
    }

    public Contains(item: T): boolean {
        return this.list.includes(item);
    }

    public Add(item: T) {
        this.list.push(item);
    }

    public AddMultiple(item: T[]) {
        this.list.push(...item);
    }

    public Insert(item: T, index: number) {
        this.list.splice(index, 0, item);
    }

    public Remove(item: T): boolean {
        const i = this.list.indexOf(item);
        if (i === -1) return false;
        this.list.splice(i, 1);
        return true;
    }

    public IndexOf(item: T): number {
        return this.list.indexOf(item);
    }

    public Get(i: number): T {
        if (i >= 0 && i < this.list.length) {
            return this.list[i];
        }
        throw new Error("Out of bounds in Array, Array size is " + this.list.length + ", requested is " + i);
    }

    public GetOrDefault(i: number): T | null {
        if (i >= 0 && i < this.list.length) {
            return this.list[i];
        }
        return null;
    }

    public Clear() {
        this.list = [];
    }

    public Count(): number {
        return this.list.length;
    }

    public First(): T {
        return this.list[0];
    }

    public FirstOrDefault(): T | null {
        if (this.list.length < 1) return null;
        return this.list[0];
    }

    public Last(): T {
        return this.list[this.list.length - 1];
    }

    public LastOrDefault(): T | null {
        if (this.list.length < 1) return null;
        return this.list[this.list.length - 1] ?? null;
    }

    public Exists(predicate: (value: T, index: number, array: T[]) => unknown): boolean {
        return this.list.some(predicate);
    }

    public ToArray(): T[] {
        return this.list;
    }

    public Clone(): List<T> {
        return new List([...this.list]);
    }
}
