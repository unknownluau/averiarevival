import { createContainer } from "unstated-next";
import { useState } from "react";

const OnBoardingStore = createContainer(() => {
    /** @type {[AssetDetailsEntry, import('react').Dispatch<AssetDetailsEntry>]} */
    const [details, setDetails] = useState(null);
    const [page, setPage] = useState(1);
    
    return {
        /** @type AssetDetailsEntry */
        details,
        setDetails,
        
        page,
        setPage,
    }
});

export default OnBoardingStore;