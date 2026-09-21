import { createUseStyles } from 'react-jss';
import {PropsWithChildren, ReactNode} from 'react';

const useStyles = createUseStyles({
    sectionContainer: {
    },
    headerContainer: {
        margin: "3px 0 6px",
        display: "flex",
        justifyContent: "space-between",
        "& h3": {
            float: "left",
            margin: 0,
            padding: "5px 0",
            fontSize: 20,
            fontWeight: 700,
            lineHeight: "1em",
            paddingTop: 0,
        },
    },
    contentContainer: {},
})

interface SectionProps extends PropsWithChildren {
    header: string;
    sectionClass?: string;
    headerContainer?: string;
    headerChildren?: ReactNode | undefined;
    headerCenter?: boolean;
    className?: string;
    contentSectioned?: boolean;
    h3Class?: string;
}

const Section = ({header, sectionClass, className, children, headerContainer, headerChildren, headerCenter, contentSectioned, h3Class}: SectionProps) => {
    const s = useStyles();

    return <div className={`${s.sectionContainer} ${sectionClass ? sectionClass : ""}`}>
        <div className={`${s.headerContainer} ${headerContainer ? headerContainer : ""} ${headerCenter ? "align-items-center" : ""}`}>
            <h3>{header}</h3>
            {headerChildren}
        </div>
        <div className={`${contentSectioned ? "section-content noShadow" : ""} ${s.contentContainer} ${className ? className : ""}`}>
            {children}
        </div>
    </div>
};

export default Section;