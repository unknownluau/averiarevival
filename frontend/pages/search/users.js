import SearchUsers from "../../components/searchUsers";
import SearchUsersNew from "../../components/searchUsersNew";
import Theme2016 from "../../components/theme2016";
import { useRouter } from "next/router";
import { useEffect, useState } from "react";
import { getSearchUserPageStyle, searchUserPageStyle } from "../../services/theme";

const SearchUsersPage = () => {
  const router = useRouter();
  const [style, setStyle] = useState(searchUserPageStyle.Modern);

  useEffect(() => {
    setStyle(getSearchUserPageStyle());
  }, []);

  if (style === searchUserPageStyle.Modern)
    return <Theme2016>
      <SearchUsersNew keyword={router.query.keyword} />
    </Theme2016>;
  return <SearchUsers keyword={router.query.keyword} />;
};

export default SearchUsersPage;
