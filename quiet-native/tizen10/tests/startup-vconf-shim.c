#include <stdlib.h>
#include <string.h>
static char home[256], startup[256], order[1024];
static int auto_start=1,fail_home,unknown_after_startup,writes;
void reset(const char *h,const char *s,int a,int fail,int unknown){strcpy(home,h);strcpy(startup,s);auto_start=a;fail_home=fail;unknown_after_startup=unknown;writes=0;order[0]=0;}
int write_count(void){return writes;}
const char *write_order(void){return order;}
const char *home_value(void){return home;}
const char *startup_value(void){return startup;}
char *vconf_get_str(const char *key){if(strstr(key,"home_appid"))return strdup(home);if(strstr(key,"startup_appid"))return strdup(startup);return NULL;}
int vconf_get_int(const char *key,int *value){*value=auto_start;return 0;}
int vconf_set_str(const char *key,const char *value){writes++;if(strstr(key,"home_appid")){strcat(order,"home;");if(fail_home)return -13;strcpy(home,value);}else if(strstr(key,"startup_appid")){strcat(order,"startup;");strcpy(startup,value);if(unknown_after_startup){strcpy(home,"external.concurrent.app");unknown_after_startup=0;}}else return -1;return 0;}
